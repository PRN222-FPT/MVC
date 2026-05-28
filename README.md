# GROUP1 Assignment 1 - MVC Document Ingestion API

Welcome to the **GROUP1 PRN222 Assignment 1** repository! This project implements a robust ASP.NET Core MVC application designed for asynchronous document ingestion (PDF/DOCX) into a RAG (Retrieval-Augmented Generation) pipeline. 

## 🏗 Architecture & Tech Stack

This project strictly adheres to a **Clean 3-Layer Architecture** to ensure separation of concerns, testability, and maintainability:

1. **MVC (Presentation Layer):** Handles HTTP requests, API endpoints (Controllers), DI configuration, Middlewares, and Razor Views.
2. **ServiceLayer (Business Logic):** Contains application logic, background workers, DTOs, and interface definitions.
3. **DataAccessLayer (Data Access):** Implements EF Core `DbContext`, `UnitOfWork`, and Generic Repositories `IRepository<T>`.

### Key Technologies
- **Framework:** .NET 10.0 (ASP.NET Core MVC & Web API)
- **Database:** PostgreSQL (Cloud-hosted via NeonDB)
- **ORM:** Entity Framework Core (Npgsql)
- **Logging:** Serilog (Structured request logging)
- **API Documentation:** Swagger UI (with XML Comments)
- **Frontend (Planned):** Bootstrap, jQuery

---

## 🚀 Current Progress & Features

The project has established a strong foundation with the following tasks **Completed**:

- [x] **Project Structure Setup:** Configured the 3-Layer architecture ensuring dependencies flow downwards (`MVC -> ServiceLayer -> DataAccessLayer`).
- [x] **Dependency Injection (DI):** Fully wired up repositories, services, options, and background tasks in `Program.cs`.
- [x] **Database & EF Core:** Configured `AppDbContext` and `Prn222Context` to connect to NeonDB. Implemented Generic Repository and Unit of Work patterns.
- [x] **Serilog Integration:** Replaced default logging with Serilog, capturing clean `Information` level logs to the console and tracking HTTP response times.
- [x] **Swagger UI:** Configured Swagger at `/swagger` to automatically pull in XML comments for beautiful API documentation.
- [x] **Document Upload API:** Implemented `POST /Documents/Upload` endpoint. Validates PDF/DOCX files, limits sizes to 20MB, saves securely to local disk (`/uploads`), and instantly returns `202 Accepted`.
- [x] **Asynchronous Processing:** Setup `IBackgroundTaskQueue` and `DocumentProcessingWorker` to handle heavy document parsing entirely in the background.
- [x] **Security Audit:** Removed all hardcoded connection string passwords from source code. App uses `dotnet user-secrets` for local development.

---

## ⚙️ Setup & Run Instructions

Follow these steps to run the project locally.

### 1. Configure the Database Connection
Since we are using a shared Cloud Database (NeonDB), **do not put the password in `appsettings.json`**. Instead, use the .NET Secret Manager. Open your terminal in the `MVC` project folder (where `MVC.csproj` is located) and run:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=ep-calm-tooth-apcrncyj-pooler.c-7.us-east-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=<PASSWORD_PROVIDED_BY_LEADER>;SSL Mode=Require;Trust Server Certificate=true"
```

### 2. Apply Migrations (If Needed)
Ensure your local environment is synced with the DB schema:
```bash
dotnet ef database update --context AppDbContext
dotnet ef database update --context Prn222Context
```

### 3. Run the Application
Start the MVC host:
```bash
dotnet run
```
- **Web App:** Available at `https://localhost:7254` (or `http://localhost:5295`)
- **Swagger API Docs:** Available at `https://localhost:7254/swagger`

---

## 🛡️ Coding Rules

Before contributing, please review the rules established in `AGENTS.md` and `ARCHITECTURE.md`.
- **Always use `IUnitOfWork`** for database transactions.
- **Keep Controllers Thin:** Move business logic to `ServiceLayer`.
- **Security:** Never commit passwords or API keys to Git.
