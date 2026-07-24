# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files, so dependency layers cache across source edits.
COPY MVC/MVC.csproj MVC/
COPY ServiceLayer/ServiceLayer.csproj ServiceLayer/
COPY DataAccessLayer/DataAccessLayer.csproj DataAccessLayer/
COPY DocumentParser/DocumentParser.csproj DocumentParser/
RUN dotnet restore MVC/MVC.csproj

COPY MVC/ MVC/
COPY ServiceLayer/ ServiceLayer/
COPY DataAccessLayer/ DataAccessLayer/
COPY DocumentParser/ DocumentParser/

RUN dotnet publish MVC/MVC.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Tesseract OCR native libraries for DocumentParser's Tesseract .NET wrapper.
# The wrapper only ships Windows DLLs (x64/tesseract50.dll, x64/leptonica-1.82.0.dll) and its
# InteropDotNet loader dlopen()s libs by that exact name from an "x64" folder next to the app —
# it does not consult ldconfig/system lib paths. So: install the real libs via apt, then symlink
# them to the filenames/location the loader expects. libdl.so is symlinked too because this base
# image (glibc >= 2.34) only ships the versioned libdl.so.2, not the unversioned name dlopen() needs.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr \
    && rm -rf /var/lib/apt/lists/* \
    && ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so

COPY --from=build /app/publish .

RUN mkdir -p /app/x64 \
    && ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "MVC.dll"]
