using System.Text;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public sealed class StudentRosterParserTests
{
    [Fact]
    public async Task ParseAsync_WithCsvGoogleSheetExport_ReturnsStudentRows()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "MSSV,Tên,Email\r\nSE001,Nguyen Van A,a@example.com\r\nSE002,Tran Thi B,b@example.com\r\n"));

        var rows = await StudentRosterParser.ParseAsync("students.csv", stream);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(2, row.RowNumber);
                Assert.Equal("SE001", row.StudentCode);
                Assert.Equal("Nguyen Van A", row.FullName);
                Assert.Equal("a@example.com", row.Email);
            },
            row =>
            {
                Assert.Equal(3, row.RowNumber);
                Assert.Equal("SE002", row.StudentCode);
                Assert.Equal("Tran Thi B", row.FullName);
                Assert.Equal("b@example.com", row.Email);
            });
    }

    [Fact]
    public async Task ParseAsync_WithQuotedCsvName_PreservesComma()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "MSSV,Name,Email\r\nSE001,\"Nguyen, Van A\",a@example.com\r\n"));

        var rows = await StudentRosterParser.ParseAsync("students.csv", stream);

        StudentImportRowDto row = Assert.Single(rows);
        Assert.Equal("Nguyen, Van A", row.FullName);
    }

    [Fact]
    public async Task ParseAsync_WithoutRequiredHeaders_ThrowsInvalidOperationException()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "Code,Name,Contact\r\nSE001,Nguyen Van A,a@example.com\r\n"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => StudentRosterParser.ParseAsync("students.csv", stream));

        Assert.Contains("MSSV", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
