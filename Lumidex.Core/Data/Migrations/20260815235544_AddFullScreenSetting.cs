using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFullScreenSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FullScreen",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullScreen",
                table: "AppSettings");
        }
    }
}
