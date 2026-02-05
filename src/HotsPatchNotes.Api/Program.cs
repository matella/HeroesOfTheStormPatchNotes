using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Heroes of the Storm Patch Notes API", Version = "v1" });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=hots.db";
builder.Services.AddDbContext<HotsDbContext>(options =>
    options.UseSqlite(connectionString));

// HttpClient for GitHub API
builder.Services.AddHttpClient<IGitHubSyncService, GitHubSyncService>();

// Background sync service
builder.Services.AddHostedService<BackgroundSyncService>();

// CORS for Blazor WebAssembly
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                    "https://localhost:7000",
                    "http://localhost:5000"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // For Docker/Production - allow all origins since web container makes requests
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
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
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorClient");
app.UseResponseCaching();
app.UseAuthorization();
app.MapControllers();

app.Run();
