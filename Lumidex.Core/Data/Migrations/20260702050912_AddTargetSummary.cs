using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScopeMerges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    AbsorbedName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeMerges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetMerges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SurvivorTargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AbsorbedTargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    SurvivorLabel = table.Column<string>(type: "TEXT", nullable: false),
                    AbsorbedLabel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetMerges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Targets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanonicalName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    SimbadId = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    Ra = table.Column<double>(type: "REAL", nullable: true),
                    Dec = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetFilterGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    Filter = table.Column<string>(type: "TEXT", nullable: false),
                    GoalHours = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetFilterGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetFilterGoals_Targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetNameMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawObjectName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetNameMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetNameMaps_Targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageFiles_Type_ObjectName",
                table: "ImageFiles",
                columns: new[] { "Type", "ObjectName" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopeMerges_AbsorbedName",
                table: "ScopeMerges",
                column: "AbsorbedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetFilterGoals_TargetId_Scope_Filter",
                table: "TargetFilterGoals",
                columns: new[] { "TargetId", "Scope", "Filter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetMerges_AbsorbedTargetId",
                table: "TargetMerges",
                column: "AbsorbedTargetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetNameMaps_RawObjectName",
                table: "TargetNameMaps",
                column: "RawObjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetNameMaps_TargetId",
                table: "TargetNameMaps",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScopeMerges");

            migrationBuilder.DropTable(
                name: "TargetFilterGoals");

            migrationBuilder.DropTable(
                name: "TargetMerges");

            migrationBuilder.DropTable(
                name: "TargetNameMaps");

            migrationBuilder.DropTable(
                name: "Targets");

            migrationBuilder.DropIndex(
                name: "IX_ImageFiles_Type_ObjectName",
                table: "ImageFiles");
        }
    }
}
