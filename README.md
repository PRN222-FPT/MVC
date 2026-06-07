# GROUP1 PRN222 MVC

ASP.NET Core MVC application for document upload, background ingestion, and RAG-based chat over uploaded learning materials.

## Architecture

The solution follows a 3-layer structure:

- `MVC/`: presentation layer, controllers, Razor views, middleware, dependency injection, hosted worker registration.
- `ServiceLayer/`: business services, DTOs, options, password hashing, email, document processing, chat orchestration.
- `DataAccessLayer/`: EF Core `Prn222Context`, entities, repositories, and Unit of Work.

Dependency direction is one-way:

```text
MVC -> ServiceLayer -> DataAccessLayer
```

![System structure](system_structure.png)

## Tech Stack

- .NET 10.0
- ASP.NET Core MVC
- PostgreSQL with Npgsql EF Core provider
- Cookie authentication
- Serilog console logging
- Swagger UI in Development
- Qdrant for vector search
- Gemini/OpenRouter-compatible chat configuration
- Local filesystem document storage under `uploads`

## Run the Project

### Prerequisites

- .NET 10 SDK.
- PostgreSQL running locally or reachable from your machine.
- Qdrant running locally if you want document ingestion and vector search to work.
- Gemini or OpenRouter API credentials if you want real AI chat/document-processing responses.

### Setup

1. Restore dependencies:

```powershell
dotnet restore GROUP1_Ass1.slnx
```

2. Configure `MVC/appsettings.json`:

- Set `ConnectionStrings:DefaultConnection` to your PostgreSQL database.
- Set `AdminSeed` if you want the startup process to create or update the admin account.
- Set `Smtp` if you need password reset or imported-student email delivery.
- Set `Qdrant` and `Gemini` for document processing, vector search, and chat.

3. Create the PostgreSQL database and schema.

Use the SQL in `database_script.sql` if you need to initialize the database manually. The application also applies a small startup compatibility update for user account columns, but it does not replace full database setup.

4. Run the MVC app:

```powershell
dotnet run --project MVC/MVC.csproj
```

5. Open one of the configured local URLs:

```text
http://localhost:5296
https://localhost:7255
```

Swagger is available in Development at:

```text
http://localhost:5296/swagger
https://localhost:7255/swagger
```

## Project Configuration

This project is currently configured to read application settings only from:

```text
MVC/appsettings.json
```

`Program.cs` clears the default ASP.NET Core configuration sources and adds only `appsettings.json`. This means `appsettings.Development.json`, user-secrets, environment variables, and command-line configuration values do not override project settings.

### Database

Configure PostgreSQL in `MVC/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=prn222;Username=admin;Password=123456"
}
```

Use Npgsql key-value format. Do not use PostgreSQL URL format such as `postgresql://user:password@host/database`, because the current EF Core setup passes the string directly to `UseNpgsql`.

Before starting the app, make sure PostgreSQL is running and the configured database/user exist.

### Admin Seed

The application seeds or updates the admin account during startup from:

```json
"AdminSeed": {
  "Email": "admin@gmail.com",
  "Password": "admin@123"
}
```

Current usable admin account:

```text
Email: admin@gmail.com
Password: admin@123
```

To change the admin account, edit `AdminSeed:Email` and `AdminSeed:Password` in `MVC/appsettings.json`, then restart the application. On startup:

- If the account does not exist, it is created with role `admin`.
- If the account exists but is not admin, its role is changed to `admin`.
- If the configured password differs from the stored hash, the password is reset to the configured value.
- If either admin email or password is blank, admin seeding is skipped.

### SMTP

Student import and password reset emails use:

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true,
  "Username": "...",
  "Password": "...",
  "FromAddress": "...",
  "FromName": "DocAssistant"
}
```

Because this project now uses only `appsettings.json`, these values must be present there for email features to work.

### Uploads

Document files are stored under the configured storage root:

```json
"Upload": {
  "MaxFileSizeBytes": 20971520,
  "AllowedExtensions": [ ".pdf", ".docx" ],
  "StorageRoot": "uploads"
}
```

Deleting the `uploads` folder does not prevent the app from starting. New uploads can recreate storage as needed, but existing database records may point to files that no longer exist.

### AI and Vector Search

Configure Qdrant and Gemini/OpenRouter-compatible settings in `MVC/appsettings.json`:

```json
"Qdrant": {
  "Host": "localhost",
  "Port": 6334,
  "Https": false,
  "CollectionName": "documents"
}
```

```json
"Gemini": {
  "ChatProvider": "OpenRouter",
  "ApiKey": "...",
  "OpenRouter": {
    "ApiKey": "...",
    "BaseUrl": "https://openrouter.ai/api/v1/chat/completions",
    "ModelName": "google/gemini-3.1-pro-preview"
  }
}
```

If the AI keys are missing, chat/document-processing features may use fallback services or fail depending on the workflow being executed.

## Roles and Permissions

Roles are defined in `ServiceLayer/DTOs/UserAccountDtos.cs`:

```text
admin
teacher
student
```

### Admin

Admin users are redirected to the admin console after login.

Admin capabilities:

- Manage users at `/AdminUsers`.
- Create `student` and `teacher` accounts.
- Import student accounts from `.csv` or `.xlsx` files up to 5 MB.
- Reset student and teacher passwords by email.
- Block student and teacher accounts.
- Manage subjects at `/Subjects`.
- Assign teacher accounts to subjects.
- Mark a teacher as head of department for a subject.

Admin restrictions:

- Admin accounts cannot be created from the user-management form.
- Admin accounts cannot be blocked from the admin console.
- An admin cannot block their own account.

### Teacher

Teacher users are redirected to the document library after login.

Teacher capabilities:

- View the document library at `/Documents/Library`.
- Upload `.pdf` and `.docx` documents at `/Documents/Upload`.
- View, inline-open, and download documents.
- Use chat at `/Chat`.
- View chat history and previous session messages.
- Change their own password.

Upload restrictions:

- A teacher must be assigned to a subject.
- Upload is allowed only for subjects where the teacher is marked as head of department.

### Student

Student users are redirected to chat after login.

Student capabilities:

- Use chat at `/Chat`.
- View chat history and previous session messages.
- View, inline-open, and download documents by document URL.
- Change their own password.

Student restrictions:

- Students cannot access the admin console.
- Students cannot upload documents.
- Students cannot manage subjects or user accounts.

## Security Notes

This repository is currently configured to use `appsettings.json` as the only configuration source. That matches the current local setup requirement, but it also means secrets such as database passwords, SMTP passwords, and API keys are stored in source-controlled configuration if committed.

For production, prefer environment variables, deployment secrets, or a secret manager instead of committing real credentials.

## Development Rules

- Keep controllers thin and move business logic into `ServiceLayer`.
- Use DTOs/view models at application boundaries.
- Use EF Core async APIs.
- Keep CSRF protection enabled for form posts.
- Use authorization attributes for protected controllers/actions.
- Do not bind EF entities directly to user-submitted forms.
- Review `AGENTS.md` and `ARCHITECTURE.md` before making significant changes.
