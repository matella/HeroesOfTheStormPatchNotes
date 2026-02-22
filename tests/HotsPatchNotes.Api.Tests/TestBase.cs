using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Tests;

/// <summary>
/// Base class for tests that need a database context.
/// Uses in-memory database for isolation.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected HotsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HotsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new HotsDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    protected HotsDbContext CreateContextWithData()
    {
        var context = CreateContext();
        SeedTestData(context);
        return context;
    }

    protected IHeroService CreateHeroService(HotsDbContext context)
    {
        var heroRepository = new HeroRepository(context);
        var patchRepository = new PatchRepository(context);
        var buildRepository = new BuildRepository(context);
        return new HeroService(heroRepository, patchRepository, buildRepository);
    }

    protected IPatchService CreatePatchService(HotsDbContext context)
    {
        var patchRepository = new PatchRepository(context);
        return new PatchService(patchRepository);
    }

    protected IBattlegroundService CreateBattlegroundService(HotsDbContext context)
    {
        var battlegroundRepository = new BattlegroundRepository(context);
        return new BattlegroundService(battlegroundRepository);
    }

    protected IS2MAHeroParserService CreateS2MAHeroParserService(HotsDbContext context, HttpClient httpClient)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<S2MAHeroParserService>.Instance;
        return new S2MAHeroParserService(context, httpClient, logger);
    }

    protected IGamedataXmlEnrichmentService CreateGamedataXmlEnrichmentService(HotsDbContext context, HttpClient httpClient)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GamedataXmlEnrichmentService>.Instance;
        return new GamedataXmlEnrichmentService(context, httpClient, logger);
    }

    protected IGamedataMapSyncService CreateGamedataMapSyncService(HotsDbContext context, HttpClient httpClient)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GamedataMapSyncService>.Instance;
        return new GamedataMapSyncService(context, httpClient, logger);
    }

    protected virtual void SeedTestData(HotsDbContext context)
    {
        // Add sample heroes
        var heroes = new List<Hero>
        {
            new Hero
            {
                Id = 1,
                ShortName = "abathur",
                Name = "Abathur",
                Role = "Specialist",
                ExpandedRole = "Support",
                Type = "Melee",
                Icon = "abathur.png",
                HyperlinkId = "Abathur",
                AttributeId = "Abat",
                ReleaseDate = new DateTime(2014, 3, 13),
                TagsJson = "[\"Specialist\",\"Utility\"]",
                Universe = "StarCraft",
                Difficulty = "Very Hard"
            },
            new Hero
            {
                Id = 2,
                ShortName = "arthas",
                Name = "Arthas",
                Role = "Warrior",
                ExpandedRole = "Tank",
                Type = "Melee",
                Icon = "arthas.png",
                HyperlinkId = "Arthas",
                AttributeId = "Arth",
                ReleaseDate = new DateTime(2014, 3, 13),
                TagsJson = "[\"Tank\",\"Crowd Control\"]",
                Universe = "Warcraft",
                Difficulty = "Medium",
                Title = "The Lich King"
            },
            new Hero
            {
                Id = 3,
                ShortName = "valla",
                Name = "Valla",
                Role = "Assassin",
                ExpandedRole = "Ranged Assassin",
                Type = "Ranged",
                Icon = "valla.png",
                HyperlinkId = "DemonHunter",
                AttributeId = "Demo",
                ReleaseDate = new DateTime(2014, 3, 13),
                TagsJson = "[\"Sustained Damage\",\"Mobile\"]",
                Universe = "Diablo",
                Difficulty = "Easy"
            }
        };

        context.Heroes.AddRange(heroes);

        // Add sample abilities for Arthas
        var abilities = new List<Ability>
        {
            new Ability
            {
                HeroId = 2,
                Name = "Frostmourne Hungers",
                Description = "Activate to make Arthas's next Basic Attack deal increased damage.",
                AbilityId = "ArthasFrostmourneHungers",
                Hotkey = "D",
                Type = "Trait",
                IsTrait = true,
                Cooldown = 12,
                Icon = "arthas_frostmournehungers.png"
            },
            new Ability
            {
                HeroId = 2,
                Name = "Death Coil",
                Description = "Deals damage to target enemy or heals Arthas.",
                AbilityId = "ArthasDeathCoil",
                Hotkey = "Q",
                Type = "Basic",
                Cooldown = 9,
                ManaCost = "50",
                Icon = "arthas_deathcoil.png"
            }
        };

        context.Abilities.AddRange(abilities);

        // Add sample talents for Arthas
        var talents = new List<Talent>
        {
            new Talent
            {
                HeroId = 2,
                Name = "Frost Presence",
                Description = "Slow enemies hit by Frozen Tempest.",
                Level = 1,
                Sort = 1,
                TooltipId = "ArthasFrostPresence",
                Icon = "arthas_frostpresence.png"
            },
            new Talent
            {
                HeroId = 2,
                Name = "Eternal Hunger",
                Description = "Increase Frostmourne Hungers damage.",
                Level = 1,
                Sort = 2,
                TooltipId = "ArthasEternalHunger",
                Icon = "arthas_eternalhunger.png"
            }
        };

        context.Talents.AddRange(talents);

        // Add sample patches
        var patches = new List<Patch>
        {
            new Patch
            {
                Id = 1,
                InternalId = "2023-12-05",
                PatchName = "December 5, 2023 Patch",
                PatchType = "Balance Update",
                GameVersion = "2.55.3",
                LiveDate = new DateTime(2023, 12, 5),
                OfficialLink = "https://heroesofthestorm.blizzard.com/en-us/blog/12345"
            },
            new Patch
            {
                Id = 2,
                InternalId = "2023-11-14",
                PatchName = "November 14, 2023 Hotfix",
                PatchType = "Hotfix",
                GameVersion = "2.55.2",
                LiveDate = new DateTime(2023, 11, 14)
            }
        };

        context.Patches.AddRange(patches);

        // Add sample patch sections
        var sections = new List<PatchSection>
        {
            new PatchSection
            {
                PatchId = 1,
                Order = 1,
                HeadingLevel = 2,
                SectionType = "Hero",
                EntityName = "Arthas",
                HeroId = 2,
                Content = "- Frostmourne Hungers (Trait)\n  - Damage increased from 99 to 110"
            }
        };

        context.PatchSections.AddRange(sections);

        // Add sample battleground
        var battlegrounds = new List<Battleground>
        {
            new Battleground
            {
                Id = 1,
                ShortName = "cursed-hollow",
                Name = "Cursed Hollow",
                MapType = "3-Lane",
                Description = "The Raven Lord rules over this dark forest.",
                Objective = "Collect tributes to curse the enemy team.",
                ObjectiveTiming = "3:00 minutes",
                IsInRotation = true,
                Universe = "Nexus"
            }
        };

        context.Battlegrounds.AddRange(battlegrounds);

        context.SaveChanges();
    }

    public virtual void Dispose()
    {
        // Cleanup if needed
    }
}
