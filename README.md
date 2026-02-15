# Heroes of the Storm Patch Notes

A modern rewrite of [heroespatchnotes.com](https://heroespatchnotes.com/) - a comprehensive database for Heroes of the Storm heroes, abilities, talents, battlegrounds, and patch history.

## 🎮 Overview

This project provides a web application to browse and search Heroes of the Storm game data, including:

- **90+ Heroes** with complete ability and talent information
- **Always-Current Talent Descriptions** - Parsed directly from game files (gamestrings), updated with every game patch
- **228+ Patches** with version history and official links
- **Battlegrounds** with objectives, mercenary camps, and strategies
- **Hero Builds** - Create and share talent builds with build codes
- **Talent Trees** organized by tier (levels 1, 4, 7, 10, 13, 16, 20)
- **Patch History** - View changes to specific heroes across patches
- **Search & Filter** by hero name, role, type, and universe

## 🏗️ Architecture

The solution consists of three projects:

```
HeroesOfTheStormPatchNotes/
├── src/
│   ├── HotsPatchNotes.Api/      # ASP.NET Core Web API (Backend)
│   ├── HotsPatchNotes.Web/      # Blazor WebAssembly (Frontend)
│   └── HotsPatchNotes.Shared/   # Shared models and DTOs
└── tests/
    ├── HotsPatchNotes.Api.Tests/
    └── HotsPatchNotes.Web.Tests/
```

### Data Flow

```
GitHub Repositories + Web Sources ──► API (Sync Service) ──► SQLite Database
                                                                      │
                                                                      ▼
                                        Blazor WebAssembly ◄── API Endpoints
```

**Data Sources**:
- **[heroespatchnotes/heroes-talents](https://github.com/heroespatchnotes/heroes-talents)** - Hero structural data (abilities, talents, icons)
- **[HeroesToolChest/heroes-data](https://github.com/HeroesToolChest/heroes-data)** - Comprehensive hero stats and **gamestrings** (talent descriptions directly from game files - always current)
- **[jamiephan/HeroesOfTheStorm_S2MA](https://github.com/jamiephan/HeroesOfTheStorm_S2MA)** - Battleground map files (authoritative metadata)
- **[heroespatchnotes/heroes-patch-data](https://github.com/heroespatchnotes/heroes-patch-data)** - Patch version tracking
- **Fandom Wiki** - Fallback for battleground descriptions
- **BlueTracker** - Official Blizzard patch notes

The sync process combines multiple sources for the most accurate and up-to-date information:
1. **heroes-talents** provides the base structure (hero roster, talent trees)
2. **heroes-data gamestrings** provide talent names and descriptions (updated with every game patch)
3. **S2MA map files** provide battleground metadata
4. **BlueTracker** provides official patch notes content

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- A modern web browser

> **Note for macOS users:** The Web app uses port 5100 (HTTP) instead of 5000 to avoid conflicts with macOS Control Center (AirPlay Receiver).

### First-Time Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/HeroesOfTheStormPatchNotes.git
   cd HeroesOfTheStormPatchNotes
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Start the API** (Terminal 1)
   ```bash
   cd src/HotsPatchNotes.Api
   dotnet run
   ```
   The API will start at `https://localhost:7001`

4. **Trigger initial data sync**
   
   On first run, the database is empty. Sync data from GitHub:
   ```bash
   # Using PowerShell
   Invoke-RestMethod -Uri "https://localhost:7001/api/sync" -Method Post -SkipCertificateCheck
   
   # Using curl
   curl -k -X POST https://localhost:7001/api/sync
   ```
   This fetches all heroes and patches from GitHub (~30 seconds).

5. **Start the Web App** (Terminal 2)
   ```bash
   cd src/HotsPatchNotes.Web
   dotnet run
   ```
   The web app will start at `https://localhost:7000`

6. **Open in browser**
   
   Navigate to `https://localhost:7000` to use the application.

### Running with Docker

The easiest way to run the entire application stack:

1. **Build and start all services**
   ```bash
   docker-compose up -d
   ```

2. **Trigger initial data sync**
   ```bash
   # Using PowerShell
   Invoke-RestMethod -Uri "http://localhost:5001/api/sync" -Method Post
   
   # Using curl
   curl -X POST http://localhost:5001/api/sync
   ```
   This fetches all heroes and patches from GitHub (~30 seconds).

3. **Access the application**
   - Web App: `http://localhost:5100`
   - API: `http://localhost:5001`
   - API Swagger: `http://localhost:5001/swagger`

4. **View logs**
   ```bash
   # All services
   docker-compose logs -f
   
   # Specific service
   docker-compose logs -f api
   docker-compose logs -f web
   ```

5. **Stop services**
   ```bash
   docker-compose down
   ```

6. **Remove data and rebuild**
   ```bash
   docker-compose down -v
   docker-compose up -d --build
   ```

**Note**: The database is persisted in a Docker volume. To reset data, use `docker-compose down -v`.

## 📖 API Documentation

The API includes Swagger documentation at `https://localhost:7001/swagger`

### Key Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/heroes` | List all heroes (supports filtering) |
| GET | `/api/heroes/{shortName}` | Get hero details with abilities/talents |
| GET | `/api/heroes/{shortName}/patches` | Get patch history for a hero |
| GET | `/api/heroes/{shortName}/builds` | Get builds for a hero |
| POST | `/api/heroes/{shortName}/builds` | Create a new build |
| GET | `/api/heroes/roles` | List available roles |
| GET | `/api/battlegrounds` | List all battlegrounds |
| GET | `/api/battlegrounds/{shortName}` | Get battleground details |
| GET | `/api/patches` | List patches (paginated) |
| GET | `/api/patches/{internalId}` | Get patch details |
| POST | `/api/sync` | Trigger full data sync from GitHub |
| POST | `/api/sync/heroes` | Sync heroes only |
| POST | `/api/sync/patches` | Sync patches only |

### Query Parameters

**Heroes List** (`/api/heroes`):
- `role` - Filter by role (e.g., "Assassin", "Tank", "Healer")
- `type` - Filter by type ("Melee" or "Ranged")
- `search` - Search by hero name

**Patches List** (`/api/patches`):
- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 20, max: 100)
- `type` - Filter by patch type

## 🔄 Data Synchronization

The API automatically syncs data from multiple sources every **6 hours** via a background service. The sync process:

1. Fetches hero data from **heroes-talents** (base structure)
2. Enriches with **heroes-data** stats and **gamestrings** (talent descriptions from game files)
3. Parses battleground data from **S2MA map files**
4. Scrapes official patch notes from **BlueTracker**

You can also trigger a manual sync:

```bash
# Full sync (heroes + patches + battlegrounds)
POST /api/sync

# Heroes only (includes heroes-data enrichment and gamestrings)
POST /api/sync/heroes

# Patches only
POST /api/sync/patches
```

## 🛠️ Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

The project includes **173 passing tests** covering:
- Repository layer (data access)
- Service layer (business logic)
- Controller layer (API endpoints)
- Gamestrings parsing and HTML sanitization
- Multi-source data integration

### Project Structure

- **HotsPatchNotes.Shared** - Domain models (`Hero`, `Ability`, `Talent`, `Patch`) and DTOs
- **HotsPatchNotes.Api** - REST API with EF Core SQLite database
- **HotsPatchNotes.Web** - Blazor WebAssembly SPA with dark theme UI

### Configuration

**API** (`src/HotsPatchNotes.Api/appsettings.json`):
- Database connection string (default: SQLite file `hots.db`)
- Logging settings

**Web** (`src/HotsPatchNotes.Web/wwwroot/appsettings.json`):
- `ApiBaseAddress` - URL of the API (default: `https://localhost:7001`)

## 📝 License

This project is for educational purposes. Heroes of the Storm and all related content are trademarks of Blizzard Entertainment.

## 🙏 Credits

- Original website: [heroespatchnotes.com](https://heroespatchnotes.com/)
- Data sources: [heroespatchnotes GitHub](https://github.com/heroespatchnotes)
- Game: [Heroes of the Storm](https://heroesofthestorm.com/) by Blizzard Entertainment
