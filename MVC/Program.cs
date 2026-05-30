using System.Reflection;
using DataAccessLayer;
using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MVC.Middlewares;
using MVC.Workers;
using Qdrant.Client;
using Serilog;
using Serilog.Events;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;
using ServiceLayer.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  Bootstrap Serilog first so any startup failure is logged to the console.
// ─────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting MVC host");

    var builder = WebApplication.CreateBuilder(args);

    // ----- Logging: route all ASP.NET Core logging through Serilog -----
    builder.Host.UseSerilog();

    // ----- MVC + API controllers -----
    builder.Services.AddControllersWithViews();

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.Name = "DocAssistant.Auth";
            options.Cookie.HttpOnly = true;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

    // ----- Swagger / OpenAPI -----
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "RAG Document Ingestion API",
            Version = "v1",
            Description = "Endpoints for uploading and tracking documents in the RAG pipeline."
        });

        // Pull XML summaries from controllers/actions into the Swagger UI.
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    });

    // ----- DbContexts (single PostgreSQL DB via DefaultConnection) -----
    // The connection string is a secret: it is NOT committed. Provide it locally via
    //   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."
    // or an environment variable / deployment secret.
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. " +
            "Set it via user-secrets or an environment variable (it is intentionally not stored in source).");
    }

    // Existing Category/Product context (clean-architecture demo).
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // RAG/document pipeline context (scaffolded from PostgreSQL).
    builder.Services.AddDbContext<Prn222Context>(options =>
        options.UseNpgsql(connectionString));

    // ----- Repositories -----
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IChunkRepository, ChunkRepository>();

    // ----- Options -----
    builder.Services.Configure<UploadOptions>(
        builder.Configuration.GetSection(UploadOptions.SectionName));
    builder.Services.Configure<ChunkingOptions>(
        builder.Configuration.GetSection(ChunkingOptions.SectionName));
    builder.Services.Configure<OcrOptions>(
        builder.Configuration.GetSection(OcrOptions.SectionName));
    builder.Services.Configure<QdrantOptions>(
        builder.Configuration.GetSection(QdrantOptions.SectionName));

    // ----- Domain services -----
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IDocumentProcessor, DocumentProcessor>();
    builder.Services.AddScoped<IRecursiveChunkingService, RecursiveChunkingService>();
    builder.Services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    
    // ----- Qdrant Vector DB services -----
    builder.Services.AddSingleton<QdrantClient>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            return new QdrantClient(host: options.Host, port: options.Port, https: options.Https, apiKey: options.ApiKey);
        }
        return new QdrantClient(host: options.Host, port: options.Port, https: options.Https);
    });
    builder.Services.AddScoped<IQdrantService, QdrantService>();

    // ----- Storage + background processing -----
    builder.Services.AddSingleton<IStorageService>(serviceProvider =>
        new LocalStorageService(
            serviceProvider.GetRequiredService<IOptions<UploadOptions>>(),
            builder.Environment.ContentRootPath));
    builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
    builder.Services.AddHostedService<DocumentProcessingWorker>();

    var app = builder.Build();

    using (IServiceScope scope = app.Services.CreateScope())
    {
        var userManagementService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        await userManagementService.EnsureAdminUserAsync(new AdminUserSeedDto(
            "admin@gmail.com",
            "PBKDF2$100000$R0lSRU9ORV9BRE1JTl9TQUxU$Idc6k7eiE+pqDI5o//p5/vhww9o0lKnCDC6TfOGwbK8="));
    }

    // ----- HTTP pipeline -----
    app.UseGlobalExceptionHandler();

    // Structured request logging (method, path, status, elapsed).
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        // Swagger UI available at /swagger.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG Document Ingestion API v1");
            options.RoutePrefix = "swagger";
        });
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    // Attribute-routed API controllers (e.g. DocumentController).
    app.MapControllers();

    // Conventional MVC routing for view-based controllers.
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}")
        .WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MVC host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
