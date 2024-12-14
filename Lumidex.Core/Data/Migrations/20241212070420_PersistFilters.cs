using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Earlier versions were not setting AppSettingsId so its value in the db is null.
            // There is only one AppSettings row so force the values to 1.
            migrationBuilder.Sql(@"UPDATE AstrobinFilters SET AppSettingsId = '1' WHERE AppSettingsId IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_AstrobinFilters_AppSettings_AppSettingsId",
                table: "AstrobinFilters");

            migrationBuilder.AlterColumn<int>(
                name: "AppSettingsId",
                table: "AstrobinFilters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersistFiltersOnExit",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PersistedFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppSettingsId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersistedFilters_AppSettings_AppSettingsId",
                        column: x => x.AppSettingsId,
                        principalTable: "AppSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedFilters_AppSettingsId",
                table: "PersistedFilters",
                column: "AppSettingsId");

            migrationBuilder.AddForeignKey(
                name: "FK_AstrobinFilters_AppSettings_AppSettingsId",
                table: "AstrobinFilters",
                column: "AppSettingsId",
                principalTable: "AppSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AstrobinFilters_AppSettings_AppSettingsId",
                table: "AstrobinFilters");

            migrationBuilder.DropTable(
                name: "PersistedFilters");

            migrationBuilder.DropColumn(
                name: "PersistFiltersOnExit",
                table: "AppSettings");

            migrationBuilder.AlterColumn<int>(
                name: "AppSettingsId",
                table: "AstrobinFilters",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_AstrobinFilters_AppSettings_AppSettingsId",
                table: "AstrobinFilters",
                column: "AppSettingsId",
                principalTable: "AppSettings",
                principalColumn: "Id");
        }
    }
}
