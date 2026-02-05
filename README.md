# Heroes of the Storm Patch Notes

A modern rewrite of [heroespatchnotes.com](https://heroespatchnotes.com/) - a comprehensive database for Heroes of the Storm heroes, abilities, talents, and patch history.

## 🎮 Overview

This project provides a web application to browse and search Heroes of the Storm game data, including:

- **90+ Heroes** with complete ability and talent information
- **228+ Patches** with version history and official links
- **Talent Trees** organized by tier (levels 1, 4, 7, 10, 13, 16, 20)
- **Search & Filter** by hero name, role, and type

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
GitHub Repositories ──► API (Sync Service) ──► SQLite Database
                                                      │
                                                      ▼
                        Blazor WebAssembly ◄── API Endpoints
```

Data is sourced from the [heroespatchnotes GitHub organization](https://github.com/heroespatchnotes):
- **[heroes-talents](https://github.com/heroespatchnotes/heroes-talents)** - Hero data (abilities, talents, icons)
- **[heroes-patch-data](https://github.com/heroespatchnotes/heroes-patch-data)** - Patch version history

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
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

The easiest way to run the entire application stack with **HTTPS support**:

1. **Generate SSL certificates** (first time only)
   ```bash
   # On Windows (PowerShell)
   .\generate-certs.ps1
   
   # On macOS/Linux
   chmod +x generate-certs.sh
   ./generate-certs.sh
   ```
   This creates self-signed certificates for both API and Web containers.

2. **Build and start all services**
   ```bash
   docker-compose up -d
   ```

3. **Trigger initial data sync**
   ```bash
   # Using PowerShell
   Invoke-RestMethod -Uri "https://localhost:7001/api/sync" -Method Post -SkipCertificateCheck
   
   # Using curl
   curl -k -X POST https://localhost:7001/api/sync
   ```
   This fetches all heroes and patches from GitHub (~30 seconds).

4. **Access the application**
   - Web App (HTTPS): `https://localhost:7000` ⭐ Recommended
   - Web App (HTTP): `http://localhost:5100` (redirects to HTTPS)
   - API (HTTPS): `https://localhost:7001`
   - API (HTTP): `http://localhost:5001`
   - API Swagger: `https://localhost:7001/swagger`

5. **View logs**
   ```bash
   # All services
   docker-compose logs -f
   
   # Specific service
   docker-compose logs -f api
   docker-compose logs -f web
   ```

6. **Stop services**
   ```bash
   docker-compose down
   ```

7. **Remove data and rebuild**
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
| GET | `/api/heroes/roles` | List available roles |
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

The API automatically syncs data from GitHub every **6 hours** via a background service. You can also trigger a manual sync:

```bash
# Full sync (heroes + patches)
POST /api/sync

# Heroes only
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
