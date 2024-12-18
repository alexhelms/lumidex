using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlotSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseCalibratedFrames",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseCalibratedFrames",
                table: "AppSettings");
        }
    }
}
