using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
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
            "Host=localhost;Database=hots;Username=postgres;Password=postgres";
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

// HttpClient for GitHub API
builder.Services.AddHttpClient<IGitHubSyncService, GitHubSyncService>();

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
app.UseResponseCaching();
app.UseAuthorization();
app.MapControllers();

app.Run();
