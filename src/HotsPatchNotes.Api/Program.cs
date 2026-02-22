using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Api.Middleware;
using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Heroes of the Storm Patch Notes API", Version = "v1" });
});

// Database configuration - supports SQLite and PostgreSQL
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<HotsDbContext>(options =>
{
    if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        var pgConnectionString = connectionString ??
            throw new InvalidOperationException("PostgreSQL connection string must be configured in ConnectionStrings:DefaultConnection");
        options.UseNpgsql(pgConnectionString);
    }
    else
    {
        var sqliteConnectionString = connectionString ?? "Data Source=hots.db";
        options.UseSqlite(sqliteConnectionString);
    }
});

// Register Repositories
builder.Services.AddScoped<IHeroRepository, HeroRepository>();
builder.Services.AddScoped<IPatchRepository, PatchRepository>();
builder.Services.AddScoped<IBuildRepository, BuildRepository>();
builder.Services.AddScoped<IBattlegroundRepository, BattlegroundRepository>();

// Register Services
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IPatchService, PatchService>();
builder.Services.AddScoped<IBattlegroundService, BattlegroundService>();

// Register HTML sanitizer
builder.Services.AddSingleton(HtmlContentService.CreateSanitizer());
builder.Services.AddScoped<IHtmlContentService, HtmlContentService>();

// Register GamestringsParser
builder.Services.AddHttpClient<IGamestringsParser, GamestringsParser>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Image Download Service
builder.Services.AddHttpClient<IImageDownloadService, ImageDownloadService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HttpClient for GitHub API, BattlegroundScraper, HeroScraper, HeroesDataSyncService, and GamedataMapSyncService
builder.Services.AddHttpClient<IBattlegroundScraper, BattlegroundScraper>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});
builder.Services.AddHttpClient<IHeroesDataSyncService, HeroesDataSyncService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});
builder.Services.AddHttpClient<IGamedataMapSyncService, GamedataMapSyncService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});
builder.Services.AddHttpClient<IS2MAHeroParserService, S2MAHeroParserService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});
builder.Services.AddHttpClient<IGamedataXmlEnrichmentService, GamedataXmlEnrichmentService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IGitHubSyncService, GitHubSyncService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});

// Background sync service
builder.Services.AddHostedService<BackgroundSyncService>();

// CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5100",
                "https://localhost:7000",
                "http://localhost:62706",
                "https://localhost:44305"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Development fallback - allow any origin
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Response caching
builder.Services.AddResponseCaching();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HotsDbContext>();
    db.Database.EnsureCreated();
}

// Global exception handling (must be first middleware to catch all exceptions)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowAll"); // Allow any origin in development
}
else
{
    app.UseCors("AllowBlazorClient");
}

app.UseHttpsRedirection();

// Serve static files (for downloaded images)
app.UseStaticFiles();

app.UseResponseCaching();
app.UseAuthorization();
app.MapControllers();

app.Run();
