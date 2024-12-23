using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameUseCalibratedFrames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseCalibratedFrames",
                table: "AppSettings",
                newName: "UseIntermediateFramesForPlots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseIntermediateFramesForPlots",
                table: "AppSettings",
                newName: "UseCalibratedFrames");
        }
    }
}
