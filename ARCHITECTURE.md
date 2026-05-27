# Architecture Overview — GROUP1_Ass1 MVC

> This document describes the layered architecture, design patterns, request lifecycle,
> error-handling strategy, and key decisions implemented in the MVC project.
> Written for **human and AI reviewers** who need to understand the codebase quickly.

---

## Table of Contents

1. [Solution Structure](#solution-structure)
2. [Layer Responsibilities](#layer-responsibilities)
3. [Dependency Flow](#dependency-flow)
4. [Design Patterns](#design-patterns)
5. [Request Lifecycle](#request-lifecycle)
6. [Error Handling Strategy](#error-handling-strategy)
7. [Dependency Injection Map](#dependency-injection-map)
8. [Key Design Decisions](#key-design-decisions)
9. [Security Considerations](#security-considerations)
10. [Testing Strategy](#testing-strategy)
11. [How to Extend](#how-to-extend)

---

## Solution Structure

```
GROUP1_Ass1.slnx
│
├── DataAccessLayer/                  ← Bottom layer (no dependencies)
│   ├── Entities/
│   │   ├── Category.cs               Domain entity
│   │   └── Product.cs                Domain entity
│   ├── Repositories/
│   │   ├── IRepository.cs            Generic repository contract
│   │   └── Repository.cs             Generic repository implementation (EF Core)
│   ├── UnitOfWork/
│   │   ├── IUnitOfWork.cs            UoW contract — exposes repos + SaveChangesAsync
│   │   └── UnitOfWork.cs             UoW implementation — lazy-init repos, shared DbContext
│   └── AppDbContext.cs               EF Core DbContext + Fluent API + Seed data
│
├── ServiceLayer/                     ← Middle layer (depends on DataAccessLayer)
│   ├── DTOs/
│   │   ├── CategoryDto.cs            Immutable record DTOs (read, create, update)
│   │   └── ProductDto.cs             Immutable record DTOs (read, create, update)
│   ├── Interfaces/
│   │   ├── ICategoryService.cs       Business logic contract
│   │   └── IProductService.cs        Business logic contract
│   └── Services/
│       ├── CategoryService.cs        Implementation — uses IUnitOfWork
│       └── ProductService.cs         Implementation — uses IUnitOfWork
│
└── MVC/                              ← Top layer (depends on ServiceLayer)
    ├── Controllers/
    │   ├── HomeController.cs          Home + Error endpoints
    │   ├── CategoryController.cs      Category CRUD — injects ICategoryService
    │   └── ProductController.cs       Product CRUD — injects IProductService + ICategoryService
    ├── Middlewares/
    │   └── GlobalExceptionMiddleware.cs  Catches all unhandled exceptions
    ├── Models/
    │   └── ErrorViewModel.cs          Error page model
    ├── ViewModels/
    │   ├── CategoryViewModels.cs      Index, Create, Edit, Details, Delete ViewModels
    │   └── ProductViewModels.cs       Index, Create, Edit, Details, Delete ViewModels
    ├── Views/                         Razor views (Category/, Product/, Shared/, Home/)
    ├── Program.cs                     Composition root — DI registration + middleware pipeline
    └── appsettings.Development.json   LocalDB connection string
```

---

## Layer Responsibilities

| Layer | Responsibility | Knows About | Never Knows About |
|-------|---------------|-------------|-------------------|
| **DataAccessLayer** | Data persistence, entity definitions, database schema, generic CRUD abstraction | EF Core, SQL Server | Services, Controllers, Views, DTOs |
| **ServiceLayer** | Business logic, validation, entity-to-DTO mapping, orchestration | DataAccessLayer entities, IUnitOfWork | Controllers, Views, ViewModels, HTTP |
| **MVC** | HTTP handling, user input validation, ViewModel binding, view rendering, DI wiring | ServiceLayer interfaces, DTOs | EF Core DbContext, SQL, Repositories |

### Why this separation matters

- **Testability**: Each layer can be unit-tested by mocking the layer below.
- **Replaceability**: Switch from SQL Server to PostgreSQL by only changing DataAccessLayer.
- **Team scalability**: Multiple developers can work on different layers with clear contracts.

---

## Dependency Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        MVC (ASP.NET Core)                       │
│  Controllers → ICategoryService / IProductService (interfaces)  │
│  Program.cs → DI registration                                  │
│  Middlewares → GlobalExceptionMiddleware                        │
└─────────────────────────┬───────────────────────────────────────┘
                          │ references (project reference)
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ServiceLayer                              │
│  Services → IUnitOfWork (interface)                             │
│  DTOs ← immutable records (data boundary)                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │ references (project reference)
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                     DataAccessLayer                             │
│  IRepository<T> / IUnitOfWork (contracts)                      │
│  Repository<T> / UnitOfWork (implementations)                  │
│  Entities / AppDbContext (EF Core)                             │
└─────────────────────────────────────────────────────────────────┘
                          │
                          ▼
                    ┌──────────┐
                    │ SQL Server│
                    └──────────┘
```

> **Rule**: Dependencies flow **downward only**. A lower layer NEVER references a higher layer.
> This prevents circular dependencies and keeps each layer independently testable.

---

## Design Patterns

### 1. Generic Repository Pattern (`IRepository<T>` / `Repository<T>`)

**What it does**: Abstracts CRUD operations behind a generic interface so that services
never interact directly with `DbSet<T>` or `DbContext`.

```csharp
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    IQueryable<T> Query();   // ← escape hatch for complex queries
}
```

**Why `Query()` exists**: The most common criticism of Generic Repository is that it limits
EF Core's query capabilities. The `Query()` method returns `IQueryable<T>`, allowing
services to compose `Include()`, `Where()`, `OrderBy()`, and `AsNoTracking()` before
materializing. This gives the best of both worlds: consistent CRUD + full LINQ power.

**When to extend**: If a specific entity needs complex queries (e.g., full-text search,
stored procedures), create a specialized repository interface:

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetTopSellingAsync(int count);
}
```

---

### 2. Unit of Work Pattern (`IUnitOfWork` / `UnitOfWork`)

**What it does**: Coordinates multiple repositories sharing a single `DbContext` instance,
ensuring all changes are committed (or rolled back) as a single atomic transaction.

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    Task<int> SaveChangesAsync();
}
```

**Why it matters**:

| Without UnitOfWork | With UnitOfWork |
|---|---|
| Each repository calls `SaveChanges` independently | All changes commit in a single `SaveChangesAsync()` call |
| Partial writes if one save fails | Atomic — all or nothing |
| Service injects 5+ repositories | Service injects 1 `IUnitOfWork` |
| Different DbContext instances may track same entity | Shared DbContext — consistent tracking |

**Lazy initialization**: Repository instances are created lazily via the null-coalescing
assignment operator (`??=`). This means repositories that aren't used in a particular
request never get instantiated.

```csharp
public IRepository<Category> Categories
    => _categories ??= new Repository<Category>(_context);
```

---

### 3. DTO Pattern (Data Transfer Objects)

**What it does**: Immutable `record` types that carry data between ServiceLayer and MVC.
Entities never leak to the presentation layer.

```csharp
// Read DTO — includes computed fields
public record CategoryDto(int CategoryId, string Name, string? Description, int ProductCount);

// Write DTOs — only fields the caller can set
public record CategoryCreateDto(string Name, string? Description);
public record CategoryUpdateDto(string Name, string? Description);
```

**Why records**: C# records provide value equality, immutability, `with` expressions,
and concise positional syntax — ideal for DTOs.

**Why separate Create/Update DTOs**: `CreateDto` has no `Id` (server generates it).
`UpdateDto` may differ from `CreateDto` in the future (e.g., some fields become read-only
after creation). Separating them now prevents breaking changes later.

---

### 4. ViewModel Pattern

**What it does**: View-specific models with `DataAnnotations` validation. Never bind EF
entities directly to forms (prevents over-posting attacks).

```csharp
public class ProductCreateViewModel
{
    [Required] public string Name { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Price { get; set; }
    public SelectList? Categories { get; set; }  // ← UI-only concern
}
```

**DTO vs ViewModel**: DTOs cross the service boundary. ViewModels cross the view boundary.
They may look similar but serve different purposes and evolve independently.

---

### 5. Global Exception Middleware

**What it does**: A single middleware at the top of the pipeline that catches all unhandled
exceptions, logs them with structured context, and returns appropriate responses.

```
Request → [GlobalExceptionMiddleware] → Routing → Controller → Service → DB
                    ↑
           catches any exception thrown downstream
```

**Behavior by environment**:

| Environment | Behavior |
|---|---|
| Development | Logs the error, then **re-throws** → ASP.NET developer exception page shows full stack trace |
| Production (MVC request) | Logs, maps to HTTP status code, **redirects** to `/Home/Error?statusCode=XXX` |
| Production (JSON/API request) | Logs, returns **JSON** `{ statusCode, message, traceId }` |

**Exception-to-status-code mapping**:

| Exception Type | HTTP Status |
|---|---|
| `KeyNotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 403 Forbidden |
| `ArgumentException` | 400 Bad Request |
| `InvalidOperationException` | 409 Conflict |
| `NotImplementedException` | 501 Not Implemented |
| `OperationCanceledException` | 499 Client Closed |
| Everything else | 500 Internal Server Error |

---

## Request Lifecycle

A typical MVC request flows through these stages:

```
                                ┌──── Pipeline ────┐
                                │                  │
Browser ──POST /Product/Create──►  GlobalException  │
                                │  Middleware       │
                                │       │          │
                                │       ▼          │
                                │  UseRouting()    │
                                │       │          │
                                │       ▼          │
                                │  UseAuthorization│
                                │       │          │
                                │       ▼          │
                                │  ProductController│
                                │   .Create(VM)    │
                                │       │          │
                                └───────┼──────────┘
                                        │
                    ┌───────────────────┘
                    ▼
            ProductController
            ├─ Validate ModelState
            ├─ Map ViewModel → CreateDto
            ├─ Call IProductService.CreateAsync(dto)
            │       │
            │       ▼
            │   ProductService
            │   ├─ Map DTO → Entity
            │   ├─ _unitOfWork.Products.AddAsync(entity)
            │   ├─ _unitOfWork.SaveChangesAsync()   ◄── atomic commit
            │   └─ Map Entity → ResponseDto
            │       │
            │       ▼
            ├─ Receive ProductDto
            └─ RedirectToAction("Index")
```

---

## Dependency Injection Map

All services are registered in `Program.cs` with **Scoped** lifetime (one instance per HTTP request):

```csharp
// DbContext — scoped by default
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// UnitOfWork — wraps a single DbContext per request
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services — one per request, receives IUnitOfWork
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
```

**Why Scoped?** EF Core's `DbContext` is not thread-safe and should be scoped to a single
request. Since `UnitOfWork` wraps `DbContext`, and services wrap `UnitOfWork`, all three
must share the same lifetime.

---

## Key Design Decisions

| Decision | Rationale | Trade-off |
|----------|-----------|-----------|
| Generic Repository | Reduces boilerplate, consistent API across entities | Adds abstraction layer; use `Query()` for complex queries |
| `IQueryable<T> Query()` on repository | Enables `Include()`, `Where()`, `OrderBy()` without bloating the interface | Leaks EF Core semantics to ServiceLayer (acceptable trade-off) |
| DTOs as `record` types | Immutable, value equality, concise syntax | Cannot use inheritance easily (rarely needed for DTOs) |
| Separate ViewModels from DTOs | ViewModels carry UI concerns (`SelectList`, display attributes); DTOs are service contracts | More classes, but prevents coupling UI to service layer |
| Middleware re-throws in Development | Preserves full developer exception page with stack trace, query details | Error page only visible in Production |
| `DeleteBehavior.Restrict` on Product→Category FK | Prevents orphaned products; explicit business rule | Must delete products before category |

---

## Security Considerations

| Area | Implementation |
|---|---|
| **CSRF** | All `[HttpPost]` actions use `[ValidateAntiForgeryToken]`; Razor form tag helper generates antiforgery token automatically |
| **Over-posting** | ViewModels with explicit properties — never bind EF entities directly to forms |
| **SQL Injection** | EF Core parameterized queries via LINQ — no string-concatenated SQL |
| **Error leakage** | Production error responses show generic messages, never stack traces or internal details |
| **Logging safety** | Exception middleware logs method/path/traceId — never logs request body, passwords, tokens, or PII |
| **Connection strings** | Development-only LocalDB string in `appsettings.Development.json`; production must use environment variables or user secrets |
| **HTML encoding** | Razor engine auto-encodes all `@Model.Property` output — no raw HTML rendering |

---

## Testing Strategy

### Unit Tests (xUnit)

| What to test | How to mock |
|---|---|
| Service methods | Mock `IUnitOfWork` and its `IRepository<T>` properties |
| Controllers | Mock `ICategoryService` / `IProductService` |
| Middleware | Use `DefaultHttpContext` + mock `RequestDelegate` |

### Integration Tests (WebApplicationFactory)

| What to test | Approach |
|---|---|
| DI wiring | Verify all services resolve without errors |
| Full request pipeline | Send HTTP requests through `TestServer`, assert responses |
| Database operations | Use EF Core `InMemory` provider or SQL Server test container |

### UI Tests (Playwright)

| What to test | Approach |
|---|---|
| CRUD workflows | Navigate to Category/Product pages, fill forms, verify results |
| Error page | Trigger a 404, verify error page renders correctly |
| Navigation | Click all navbar links, verify routing |

---

## How to Extend

### Adding a new entity (e.g., `Order`)

1. **DataAccessLayer**:
   - Create `Entities/Order.cs`
   - Add `DbSet<Order>` to `AppDbContext`
   - Add `IRepository<Order> Orders` to `IUnitOfWork` interface and implementation
   - Add Fluent API configuration in `OnModelCreating`

2. **ServiceLayer**:
   - Create `DTOs/OrderDto.cs` (read, create, update records)
   - Create `Interfaces/IOrderService.cs`
   - Create `Services/OrderService.cs` (inject `IUnitOfWork`)

3. **MVC**:
   - Create `ViewModels/OrderViewModels.cs`
   - Create `Controllers/OrderController.cs` (inject `IOrderService`)
   - Create `Views/Order/` (Index, Create, Edit, Details, Delete)
   - Register `IOrderService` in `Program.cs`
   - Add navigation link in `_Layout.cshtml`

4. **Database**: Create and apply EF migration.

### Adding a specialized repository

```csharp
// 1. Define interface
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> SearchAsync(string keyword);
}

// 2. Implement
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> SearchAsync(string keyword)
        => await _dbSet.Where(p => p.Name.Contains(keyword)).ToListAsync();
}

// 3. Update IUnitOfWork
IProductRepository Products { get; }  // change from IRepository<Product>
```
