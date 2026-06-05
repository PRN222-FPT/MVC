using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ServiceLayer.DTOs;

namespace ServiceLayer.Services;

internal static class StudentRosterParser
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static async Task<IReadOnlyList<StudentImportRowDto>> ParseAsync(
        string fileName,
        Stream fileContent,
        CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await ParseCsvAsync(fileContent, cancellationToken),
            ".xlsx" => ParseXlsx(fileContent),
            _ => throw new InvalidOperationException("Only .csv and .xlsx files are supported.")
        };
    }

    private static async Task<IReadOnlyList<StudentImportRowDto>> ParseCsvAsync(
        Stream fileContent,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileContent, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            rows.Add(ParseCsvLine(line).ToArray());
        }

        return ConvertRows(rows);
    }

    private static IReadOnlyList<StudentImportRowDto> ParseXlsx(Stream fileContent)
    {
        using var archive = new ZipArchive(fileContent, ZipArchiveMode.Read, leaveOpen: true);
        string[] sharedStrings = ReadSharedStrings(archive);
        ZipArchiveEntry worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException("The workbook does not contain a first worksheet.");

        using Stream worksheetStream = worksheet.Open();
        XDocument document = XDocument.Load(worksheetStream);
        var rows = new List<string[]>();

        foreach (XElement row in document.Descendants(SpreadsheetNamespace + "row"))
        {
            var cellsByColumn = new Dictionary<int, string>();
            foreach (XElement cell in row.Elements(SpreadsheetNamespace + "c"))
            {
                int columnIndex = GetColumnIndex((string?)cell.Attribute("r"));
                if (columnIndex < 0)
                {
                    continue;
                }

                cellsByColumn[columnIndex] = ReadCellValue(cell, sharedStrings);
            }

            if (cellsByColumn.Count == 0)
            {
                rows.Add([]);
                continue;
            }

            int maxColumn = cellsByColumn.Keys.Max();
            string[] values = new string[maxColumn + 1];
            for (int i = 0; i <= maxColumn; i++)
            {
                values[i] = cellsByColumn.TryGetValue(i, out string? value) ? value : string.Empty;
            }

            rows.Add(values);
        }

        return ConvertRows(rows);
    }

    private static IReadOnlyList<StudentImportRowDto> ConvertRows(IReadOnlyList<string[]> rows)
    {
        int headerIndex = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            throw new InvalidOperationException("The uploaded file is empty.");
        }

        string[] header = rows[headerIndex];
        int studentCodeIndex = FindHeaderIndex(header, "mssv", "studentcode", "studentid", "masv", "masinhvien");
        int fullNameIndex = FindHeaderIndex(header, "ten", "name", "fullname", "hoten", "hovaten", "studentname");
        int emailIndex = FindHeaderIndex(header, "email", "mail", "emailaddress");

        if (studentCodeIndex < 0 || fullNameIndex < 0 || emailIndex < 0)
        {
            throw new InvalidOperationException("The file must contain MSSV, name, and email columns.");
        }

        var students = new List<StudentImportRowDto>();
        for (int rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            string[] row = rows[rowIndex];
            string studentCode = GetValue(row, studentCodeIndex);
            string fullName = GetValue(row, fullNameIndex);
            string email = GetValue(row, emailIndex);

            if (string.IsNullOrWhiteSpace(studentCode)
                && string.IsNullOrWhiteSpace(fullName)
                && string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            students.Add(new StudentImportRowDto(rowIndex + 1, studentCode, fullName, email));
        }

        return students;
    }

    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cell, string[] sharedStrings)
    {
        string type = (string?)cell.Attribute("t") ?? string.Empty;
        XElement? valueElement = cell.Element(SpreadsheetNamespace + "v");

        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)).Trim();
        }

        string value = valueElement?.Value ?? string.Empty;
        if (type == "s"
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Length)
        {
            return sharedStrings[sharedStringIndex].Trim();
        }

        return value.Trim();
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        int index = 0;
        foreach (char character in cellReference.TakeWhile(char.IsLetter))
        {
            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return index - 1;
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static int FindHeaderIndex(string[] header, params string[] acceptedNames)
    {
        for (int i = 0; i < header.Length; i++)
        {
            string normalized = NormalizeHeader(header[i]);
            if (acceptedNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string value)
    {
        string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]", string.Empty);
    }

    private static string GetValue(string[] row, int index) =>
        index >= 0 && index < row.Length ? row[index].Trim() : string.Empty;
}
