# Battleground Test Fixtures

## Overview

This folder contains HTML fixtures captured from the Heroes of the Storm Fandom wiki. These fixtures are used for testing the battleground scraper without making live HTTP requests.

## Fixtures

### List/battleground-list.html
- **Source**: https://heroesofthestorm.fandom.com/wiki/Battleground
- **Downloaded**: 2026-02-09
- **Purpose**: Test parsing of the main battleground table
- **Contains**: Sample rows from the battleground table (3 examples: Alterac Pass, Cursed Hollow, Dragon Shire)
- **Tests**: 11 list parsing tests + 10 helper method tests

### Details/alterac-pass-detail.html ✅
- **Source**: https://heroesofthestorm.fandom.com/wiki/Alterac_Pass
- **Created**: 2026-02-09
- **Purpose**: Test parsing of individual battleground detail pages
- **Contains**: Description, objectives, timing, merc camps, boss info, tips, and image
- **Tests**: 7 detail extraction tests

### EdgeCases/missing-fields.html ✅
- **Created**: 2026-02-09
- **Purpose**: Test graceful handling of missing/incomplete data
- **Contains**: Minimal HTML with no content sections
- **Tests**: 1 edge case test (ensures no crashes on missing data)

### EdgeCases/malformed-html.html ✅
- **Created**: 2026-02-09
- **Purpose**: Test parser robustness with broken HTML
- **Contains**: Unclosed tags and incomplete structures
- **Tests**: 1 edge case test (ensures HtmlAgilityPack handles errors)

## Test Coverage Summary

**Total: 30 Battleground Scraper Tests**

### List Parsing (11 tests)
- Battleground count
- Name extraction
- Short name generation
- Lanes parsing
- Universe/realm mapping
- Objective summary
- Wiki URL generation
- Thumbnail URLs (graceful null handling)
- Release date parsing

### Detail Parsing (7 tests)
- Description extraction
- Objective details
- Objective timing
- Mercenary camps
- Boss information
- Strategy tips
- High-resolution image URLs

### Helper Methods (10 tests)
- `GenerateShortName()` - 6 test cases
- `MapRealmToUniverse()` - 4 test cases

### Edge Cases (2 tests)
- Missing fields handling
- Malformed HTML handling

## Updating Fixtures

When Fandom updates their HTML structure:

1. Visit the source URL in a browser
2. Save the page HTML (Ctrl+S or "Save Page As")  
3. Simplify: Remove scripts, styles, ads, navigation - keep core structure
4. Replace the fixture file
5. Run tests to verify parsing still works: `dotnet test --filter "BattlegroundScraperTests"`
6. If tests fail, update the XPath selectors in `BattlegroundScraper.cs`

## Notes

- ✅ Fixtures are simplified versions of full pages (unnecessary scripts/styles removed)
- ✅ Focus on preserving the table structure and key elements
- ✅ Keep file sizes reasonable (remove ads, navigation, etc.)
- ✅ All 30 tests passing
- ✅ Tests run in <1 second (no network calls)
- ✅ Works completely offline

## Why Heroes Don't Need Fixtures

Heroes are loaded from GitHub as structured JSON files (not HTML), so they don't require HTML scraping fixtures. The `GitHubSyncService` fetches JSON directly from:
- `https://raw.githubusercontent.com/heroespatchnotes/heroes-talents/master/hero/{hero-name}.json`

This means hero data parsing is already reliable and doesn't need the same fixture-based testing approach.

