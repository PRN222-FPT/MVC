using System.Reflection;
using DataAccessLayer;
using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MVC.Middlewares;
using MVC.Workers;
using Serilog;
using Serilog.Events;
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

    // ----- Domain services -----
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IDocumentProcessor, DocumentProcessor>();
    builder.Services.AddScoped<IRecursiveChunkingService, RecursiveChunkingService>();

    // ----- Storage + background processing -----
    builder.Services.AddSingleton<IStorageService, LocalStorageService>();
    builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
    builder.Services.AddHostedService<DocumentProcessingWorker>();

    var app = builder.Build();

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

    app.UseAuthorization();

    app.MapStaticAssets();

    // Attribute-routed API controllers (e.g. DocumentController).
    app.MapControllers();

    // Conventional MVC routing for view-based controllers.
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
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
