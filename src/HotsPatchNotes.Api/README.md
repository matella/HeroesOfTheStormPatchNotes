# HotsPatchNotes.Api

ASP.NET Core Web API backend for the Heroes of the Storm Patch Notes application.

## Overview

This project provides a REST API that:
- Stores hero and patch data in a SQLite database
- Syncs data from GitHub repositories ([heroes-talents](https://github.com/heroespatchnotes/heroes-talents), [heroes-patch-data](https://github.com/heroespatchnotes/heroes-patch-data))
- Exposes endpoints for the Blazor WebAssembly frontend
- Runs background sync every 6 hours to keep data fresh

## Getting Started

### Prerequisites

- .NET 10 SDK or later

### Running the API

```bash
cd src/HotsPatchNotes.Api
dotnet run
```

The API will start at:
- **HTTPS**: `https://localhost:7001`
- **HTTP**: `http://localhost:5001`

### First-Time Setup

On first run, the SQLite database (`hots.db`) is created automatically but is empty. You must trigger an initial sync:

```bash
# PowerShell
Invoke-RestMethod -Uri "https://localhost:7001/api/sync" -Method Post -SkipCertificateCheck

# curl
curl -k -X POST https://localhost:7001/api/sync

# Or use Swagger UI
# Navigate to https://localhost:7001/swagger and use the POST /api/sync endpoint
```

The sync takes approximately 30-60 seconds to fetch all 90+ heroes and 228+ patches.

## API Endpoints

### Heroes

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/heroes` | List all heroes |
| GET | `/api/heroes/{shortName}` | Get hero with abilities and talents |
| GET | `/api/heroes/roles` | List distinct hero roles |

**Query Parameters for `/api/heroes`:**
- `role` (string) - Filter by role (e.g., "Assassin", "Tank", "Healer", "Support")
- `type` (string) - Filter by type ("Melee" or "Ranged")
- `search` (string) - Search by hero name or short name

**Examples:**
```bash
# Get all heroes
GET /api/heroes

# Get assassins only
GET /api/heroes?role=Assassin

# Search for heroes with "rag" in name
GET /api/heroes?search=rag

# Get a specific hero
GET /api/heroes/ragnaros
```

### Patches

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/patches` | List patches (paginated) |
| GET | `/api/patches/{internalId}` | Get patch details |
| GET | `/api/patches/types` | List distinct patch types |

**Query Parameters for `/api/patches`:**
- `page` (int) - Page number, default: 1
- `pageSize` (int) - Items per page, default: 20, max: 100
- `type` (string) - Filter by patch type (e.g., "Balance Update", "Major Patch")

**Examples:**
```bash
# Get first page of patches
GET /api/patches

# Get page 2 with 50 items
GET /api/patches?page=2&pageSize=50

# Get only balance updates
GET /api/patches?type=Balance%20Update
```

### Sync

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sync` | Full sync (heroes + patches) |
| POST | `/api/sync/heroes` | Sync heroes only |
| POST | `/api/sync/patches` | Sync patches only |

**Response:**
```json
{
  "success": true,
  "message": "Sync completed",
  "heroesUpdated": 90,
  "patchesUpdated": 228,
  "syncedAt": "2024-01-15T10:30:00Z",
  "errors": []
}
```

## Project Structure

```
HotsPatchNotes.Api/
├── Controllers/
│   ├── HeroesController.cs    # Hero endpoints
│   ├── PatchesController.cs   # Patch endpoints
│   └── SyncController.cs      # Data sync endpoints
├── Data/
│   └── HotsDbContext.cs       # EF Core database context
├── Services/
│   ├── GitHubSyncService.cs   # Fetches data from GitHub
│   └── BackgroundSyncService.cs # Periodic sync (every 6 hours)
├── Program.cs                 # App configuration
└── appsettings.json           # Configuration file
```

## Database

The API uses **SQLite** with Entity Framework Core. The database file (`hots.db`) is created in the project directory.

### Schema

**Heroes Table:**
- Id, ShortName, Name, Icon, Role, ExpandedRole, Type, ReleaseDate, Tags, etc.

**Abilities Table:**
- Id, HeroId (FK), Name, Description, Hotkey, Cooldown, ManaCost, Icon, Type, FormName

**Talents Table:**
- Id, HeroId (FK), Level (1/4/7/10/13/16/20), Name, Description, Icon, Sort order

**Patches Table:**
- Id, InternalId, PatchName, PatchType, GameVersion, LiveDate, OfficialLink, etc.

### Resetting the Database

To reset the database, simply delete `hots.db` and restart the API. Then trigger a sync.

```bash
# Windows
del hots.db

# Linux/macOS
rm hots.db
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### CORS

CORS is currently configured to allow any origin for development:

```csharp
policy.AllowAnyOrigin()
      .AllowAnyHeader()
      .AllowAnyMethod();
```

**For production**, you should restrict CORS to specific origins by modifying `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5000",
            "https://localhost:7000",
            "https://your-production-url.com"  // Add your URL
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});
```

## Background Sync Service

The `BackgroundSyncService` runs automatically and syncs data from GitHub every 6 hours. This ensures the database stays up-to-date without manual intervention.

To change the sync interval, modify `BackgroundSyncService.cs`:

```csharp
private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(6); // Change this
```

## Swagger / OpenAPI

API documentation is available at `https://localhost:7001/swagger` when running in Development mode.

## Dependencies

- **Microsoft.EntityFrameworkCore.Sqlite** - SQLite database provider
- **Microsoft.EntityFrameworkCore.Design** - EF Core tooling
- **Swashbuckle.AspNetCore** - Swagger/OpenAPI documentation

## Troubleshooting

### "No heroes found" after starting
The database starts empty. Run a sync: `POST /api/sync`

### SSL Certificate errors
In development, use `-SkipCertificateCheck` (PowerShell) or `-k` (curl) to skip certificate validation.

### Sync fails with timeout
GitHub API rate limits may apply. Wait a few minutes and try again, or sync heroes and patches separately.

### Database locked errors
Ensure only one instance of the API is running. Stop any other processes using `hots.db`.
