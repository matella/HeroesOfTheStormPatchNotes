# HotsPatchNotes.Web

Blazor WebAssembly frontend for the Heroes of the Storm Patch Notes application.

## Overview

This project provides a modern, responsive single-page application (SPA) for browsing Heroes of the Storm game data:

- **Hero Browser** - Grid view of all heroes with search and filtering
- **Hero Details** - Complete ability and talent tree information
- **Patch History** - Paginated list of all game patches
- **Dark Theme** - Modern dark UI matching the game's aesthetic

## Getting Started

### Prerequisites

- .NET 8 SDK or later
- The API must be running (see [HotsPatchNotes.Api](../HotsPatchNotes.Api/README.md))

### Running the Web App

1. **Ensure the API is running first**
   ```bash
   cd src/HotsPatchNotes.Api
   dotnet run
   ```

2. **Start the Web App**
   ```bash
   cd src/HotsPatchNotes.Web
   dotnet run
   ```

The web app will start at:
- **HTTPS**: `https://localhost:7000`
- **HTTP**: `http://localhost:5000`

3. **Open in browser**
   
   Navigate to `https://localhost:7000`

## Pages

### Home (`/`)

The main hero browser featuring:
- **Hero Grid** - All heroes displayed as cards with icons
- **Search Bar** - Filter heroes by name
- **Role Filter** - Filter by role (Assassin, Tank, Healer, etc.)
- **Type Filter** - Filter by Melee or Ranged

Click any hero card to view their details.

### Hero Detail (`/hero/{shortName}`)

Detailed view of a specific hero showing:
- **Hero Info** - Name, role, type, release date
- **Abilities** - Grouped by form (e.g., Abathur has base abilities and Symbiote abilities)
- **Talent Tree** - All talents organized by tier (1, 4, 7, 10, 13, 16, 20)

Example URLs:
- `/hero/abathur`
- `/hero/ragnaros`
- `/hero/li-ming`

### Patches (`/patches`)

Paginated list of all game patches showing:
- Patch name and type
- Game version
- Release date
- Links to official patch notes

Use the pagination controls to navigate through the patch history.

### About (`/about`)

Information about the project, data sources, and credits.

## Project Structure

```
HotsPatchNotes.Web/
├── Layout/
│   ├── MainLayout.razor       # Main page layout with header/footer
│   └── MainLayout.razor.css   # Layout styles
├── Pages/
│   ├── Home.razor             # Hero browser page
│   ├── Home.razor.css
│   ├── HeroDetail.razor       # Hero detail page
│   ├── HeroDetail.razor.css
│   ├── Patches.razor          # Patch history page
│   ├── Patches.razor.css
│   ├── About.razor            # About page
│   └── About.razor.css
├── Services/
│   ├── HeroService.cs         # API client for hero endpoints
│   └── PatchService.cs        # API client for patch endpoints
├── wwwroot/
│   ├── css/
│   │   └── app.css            # Global styles
│   ├── appsettings.json       # Configuration (API URL)
│   └── index.html             # HTML host page
├── _Imports.razor             # Global Razor imports
├── App.razor                  # Root component with routing
└── Program.cs                 # App startup and DI configuration
```

## Configuration

### API Base Address

The API URL is configured in `wwwroot/appsettings.json`:

```json
{
  "ApiBaseAddress": "https://localhost:7001"
}
```

Change this to point to your API server:

```json
{
  "ApiBaseAddress": "https://your-api-server.com"
}
```

### Changing at Runtime

The configuration is loaded at startup. To change the API URL, modify `appsettings.json` and refresh the browser.

## Services

### IHeroService

Provides methods for fetching hero data:

```csharp
Task<List<HeroSummaryDto>> GetHeroesAsync(string? role = null, string? type = null, string? search = null);
Task<HeroDetailDto?> GetHeroAsync(string shortName);
Task<List<string>> GetRolesAsync();
```

### IPatchService

Provides methods for fetching patch data:

```csharp
Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(int page = 1, int pageSize = 20, string? type = null);
Task<PatchDetailDto?> GetPatchAsync(string internalId);
Task<List<string>> GetPatchTypesAsync();
```

## Styling

The application uses a **dark theme** with CSS custom properties for easy customization.

### Color Scheme

```css
:root {
    --bg-primary: #0a0a0f;      /* Main background */
    --bg-secondary: #12121a;    /* Card backgrounds */
    --bg-tertiary: #1a1a2e;     /* Hover states */
    --text-primary: #e0e0e0;    /* Main text */
    --text-secondary: #a0a0a0;  /* Secondary text */
    --accent-blue: #4fc3f7;     /* Links and accents */
    --accent-gold: #ffd700;     /* Highlights */
}
```

### Customizing Styles

- **Global styles**: `wwwroot/css/app.css`
- **Layout styles**: `Layout/MainLayout.razor.css`
- **Page-specific styles**: Each page has a corresponding `.css` file

## Hero Images

Hero and ability icons are loaded directly from the GitHub repository:

```
https://raw.githubusercontent.com/heroespatchnotes/heroes-talents/master/images/heroes/{icon}
```

No local image storage is required - images are fetched on demand.

## Building for Production

```bash
dotnet publish -c Release
```

The output will be in `bin/Release/net8.0/publish/wwwroot/`. This is a static site that can be hosted on any web server.

### Hosting Options

- **Azure Static Web Apps**
- **GitHub Pages**
- **Netlify**
- **Any static file server (nginx, Apache, IIS)**

## Browser Compatibility

Blazor WebAssembly requires a modern browser with WebAssembly support:
- Chrome 57+
- Firefox 52+
- Safari 11+
- Edge 16+

## Troubleshooting

### "Loading..." never completes
- Check browser console for errors
- Verify the API is running and accessible
- Check `appsettings.json` has the correct API URL

### CORS errors in console
- Ensure the API has CORS configured for the web app's URL
- Check that both HTTP and HTTPS origins are allowed

### Heroes not loading
- Verify the API has data (trigger a sync if needed)
- Check network tab for failed API requests

### Slow initial load
Blazor WebAssembly downloads the .NET runtime on first visit. Subsequent visits use cached files for faster loading.

## Dependencies

- **Microsoft.AspNetCore.Components.WebAssembly** - Blazor WebAssembly runtime
- **Microsoft.AspNetCore.Components.WebAssembly.DevServer** - Development server
- **HotsPatchNotes.Shared** - Shared models and DTOs
