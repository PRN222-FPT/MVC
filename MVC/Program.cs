using System.Reflection;
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

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    });

    // ----- DbContexts -----
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. " +
            "Set it via appsettings, user-secrets, or an environment variable.");
    }

    builder.Services.AddDbContext<Prn222Context>(options =>
        options.UseNpgsql(connectionString));

    // ----- Repositories -----
    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
    builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

    // ----- Options -----
    builder.Services.Configure<UploadOptions>(
        builder.Configuration.GetSection(UploadOptions.SectionName));
    builder.Services.Configure<ChunkingOptions>(
        builder.Configuration.GetSection(ChunkingOptions.SectionName));
    builder.Services.Configure<OcrOptions>(
        builder.Configuration.GetSection(OcrOptions.SectionName));
    builder.Services.Configure<QdrantOptions>(
        builder.Configuration.GetSection(QdrantOptions.SectionName));
    builder.Services.Configure<GeminiOptions>(
        builder.Configuration.GetSection(GeminiOptions.SectionName));
    builder.Services.Configure<SmtpOptions>(
        builder.Configuration.GetSection(SmtpOptions.SectionName));
    IConfigurationSection geminiSection = builder.Configuration.GetSection(GeminiOptions.SectionName);
    string chatProvider = geminiSection["ChatProvider"] ?? GeminiChatProviders.Google;
    string? geminiApiKey = geminiSection["ApiKey"];
    string? openRouterApiKey = geminiSection.GetSection(nameof(GeminiOptions.OpenRouter))["ApiKey"];

    // ----- Domain services -----
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IDocumentProcessor, DocumentProcessor>();
    builder.Services.AddScoped<IRecursiveChunkingService, RecursiveChunkingService>();
    if (string.IsNullOrWhiteSpace(geminiApiKey))
    {
        Log.Warning("Gemini:ApiKey is not configured. Document processing will fail fast before Qdrant upsert.");
        builder.Services.AddScoped<IEmbeddingService, NoOpEmbeddingService>();
    }
    else
    {
        builder.Services.AddSingleton<Google.GenAI.Client>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            return new Google.GenAI.Client(apiKey: options.ApiKey);
        });
        builder.Services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
    }
    builder.Services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<ICitationService, CitationService>();
    builder.Services.AddScoped<ISubjectService, SubjectService>();

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

    // ----- Gemini-compatible chat services -----
    if (chatProvider.Equals(GeminiChatProviders.OpenRouter, StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(openRouterApiKey))
        {
            Log.Warning("Gemini:OpenRouter:ApiKey is not configured. Chat will use a development fallback response.");
            builder.Services.AddScoped<IGeminiService, NoOpGeminiService>();
        }
        else
        {
            builder.Services.AddHttpClient<IGeminiService, OpenRouterGeminiService>();
        }
    }
    else if (string.IsNullOrWhiteSpace(geminiApiKey))
    {
        Log.Warning("Gemini:ApiKey is not configured. Chat will use a development fallback response.");
        builder.Services.AddScoped<IGeminiService, NoOpGeminiService>();
    }
    else
    {
        builder.Services.AddScoped<IGeminiService, GeminiService>();
    }
    builder.Services.AddScoped<IRetrievalService, RetrievalService>();
    builder.Services.AddScoped<IChatService, ChatService>();

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
        var context = scope.ServiceProvider.GetRequiredService<Prn222Context>();
        await EnsureUserSchemaCompatibilityAsync(context);

        AdminUserSeedDto? adminSeed = GetConfiguredAdminSeed(builder.Configuration);
        if (adminSeed is not null)
        {
            var userManagementService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            await userManagementService.EnsureAdminUserAsync(adminSeed);
        }
        else
        {
            Log.Warning("Admin seed skipped because AdminSeed:Email or AdminSeed:Password is not configured.");
        }
    }

    // ----- HTTP pipeline -----
    app.UseGlobalExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
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
    app.MapControllers();
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

static async Task EnsureUserSchemaCompatibilityAsync(Prn222Context context)
{
    const string sql = """
        ALTER TABLE users
            ADD COLUMN IF NOT EXISTS is_blocked boolean NOT NULL DEFAULT false;

        ALTER TABLE users
            ADD COLUMN IF NOT EXISTS student_code character varying(50);

        CREATE UNIQUE INDEX IF NOT EXISTS users_student_code_key
            ON users (student_code)
            WHERE student_code IS NOT NULL;
        """;

    await context.Database.ExecuteSqlRawAsync(sql);
}

static AdminUserSeedDto? GetConfiguredAdminSeed(IConfiguration configuration)
{
    string? email = configuration["AdminSeed:Email"];
    string? password = configuration["AdminSeed:Password"];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return null;
    }

    return new AdminUserSeedDto(email, password);
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
