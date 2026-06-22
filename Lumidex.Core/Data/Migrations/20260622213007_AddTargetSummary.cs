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
                name: "Targets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanonicalName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Targets", x => x.Id);
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
                name: "TargetNameMaps");

            migrationBuilder.DropTable(
                name: "Targets");
        }
    }
}
