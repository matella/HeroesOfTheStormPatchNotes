using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotsPatchNotes.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPatchSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Heroes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HyperlinkId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AttributeId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExpandedRole = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReleasePatch = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heroes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PatchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PatchType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FullVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OfficialLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlternateLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LiveDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LiveBuild = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PtrOfficialLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PtrDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PtrBuild = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Abilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Uid = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Hotkey = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AbilityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Cooldown = table.Column<double>(type: "REAL", nullable: true),
                    ManaCost = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsTrait = table.Column<bool>(type: "INTEGER", nullable: false),
                    FormName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abilities_Heroes_HeroId",
                        column: x => x.HeroId,
                        principalTable: "Heroes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Talents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    TooltipId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TalentTreeId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Sort = table.Column<int>(type: "INTEGER", nullable: false),
                    Cooldown = table.Column<double>(type: "REAL", nullable: true),
                    AbilityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AbilityLinksJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Talents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Talents_Heroes_HeroId",
                        column: x => x.HeroId,
                        principalTable: "Heroes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatchSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatchSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatchSections_Heroes_HeroId",
                        column: x => x.HeroId,
                        principalTable: "Heroes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PatchSections_Patches_PatchId",
                        column: x => x.PatchId,
                        principalTable: "Patches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_HeroId_AbilityId",
                table: "Abilities",
                columns: new[] { "HeroId", "AbilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_ShortName",
                table: "Heroes",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patches_InternalId",
                table: "Patches",
                column: "InternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patches_LiveDate",
                table: "Patches",
                column: "LiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatchSections_HeroId",
                table: "PatchSections",
                column: "HeroId");

            migrationBuilder.CreateIndex(
                name: "IX_PatchSections_PatchId_SectionType_EntityName",
                table: "PatchSections",
                columns: new[] { "PatchId", "SectionType", "EntityName" });

            migrationBuilder.CreateIndex(
                name: "IX_Talents_HeroId_Level_Sort",
                table: "Talents",
                columns: new[] { "HeroId", "Level", "Sort" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abilities");

            migrationBuilder.DropTable(
                name: "PatchSections");

            migrationBuilder.DropTable(
                name: "Talents");

            migrationBuilder.DropTable(
                name: "Patches");

            migrationBuilder.DropTable(
                name: "Heroes");
        }
    }
}
