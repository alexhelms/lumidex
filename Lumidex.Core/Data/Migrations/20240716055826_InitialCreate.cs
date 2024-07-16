using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedOn = table.Column<DateTime>(type: "DATETIME", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedOn = table.Column<DateTime>(type: "DATETIME", nullable: true),
                    Name = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    LastScan = table.Column<DateTime>(type: "DATETIME", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ObjectAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ObjectName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Alias = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedOn = table.Column<DateTime>(type: "DATETIME", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedOn = table.Column<DateTime>(type: "DATETIME", nullable: true),
                    Name = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "#ffffffff")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderHash = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Path = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "DATETIME", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedOn = table.Column<DateTime>(type: "DATETIME", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CameraName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    Exposure = table.Column<double>(type: "REAL", nullable: true),
                    CameraTemperatureSetPoint = table.Column<double>(type: "REAL", nullable: true),
                    CameraTemperature = table.Column<double>(type: "REAL", nullable: true),
                    CameraGain = table.Column<int>(type: "INTEGER", nullable: true),
                    CameraOffset = table.Column<int>(type: "INTEGER", nullable: true),
                    Binning = table.Column<int>(type: "INTEGER", nullable: true),
                    PixelSize = table.Column<double>(type: "REAL", nullable: true),
                    ReadoutMode = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    FocuserName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    FocuserPosition = table.Column<int>(type: "INTEGER", nullable: true),
                    FocuserTemperature = table.Column<double>(type: "REAL", nullable: true),
                    RotatorName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    RotatorPosition = table.Column<double>(type: "REAL", nullable: true),
                    FilterWheelName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    FilterName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    MountName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    RightAscension = table.Column<double>(type: "REAL", nullable: true),
                    Declination = table.Column<double>(type: "REAL", nullable: true),
                    Altitude = table.Column<double>(type: "REAL", nullable: true),
                    Azimuth = table.Column<double>(type: "REAL", nullable: true),
                    FocalLength = table.Column<double>(type: "REAL", nullable: true),
                    Airmass = table.Column<double>(type: "REAL", nullable: true),
                    ObservationTimestampUtc = table.Column<DateTime>(type: "DATETIME", nullable: true),
                    ObservationTimestampLocal = table.Column<DateTime>(type: "DATETIME", nullable: true),
                    ObjectName = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Elevation = table.Column<double>(type: "REAL", nullable: true),
                    DewPoint = table.Column<double>(type: "REAL", nullable: true),
                    Humidity = table.Column<double>(type: "REAL", nullable: true),
                    Pressure = table.Column<double>(type: "REAL", nullable: true),
                    Temperature = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageFiles_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageFileTag",
                columns: table => new
                {
                    ImageFilesId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageFileTag", x => new { x.ImageFilesId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ImageFileTag_ImageFiles_ImageFilesId",
                        column: x => x.ImageFilesId,
                        principalTable: "ImageFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImageFileTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageFiles_HeaderHash",
                table: "ImageFiles",
                column: "HeaderHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImageFiles_LibraryId",
                table: "ImageFiles",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageFiles_Path",
                table: "ImageFiles",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ImageFileTag_TagsId",
                table: "ImageFileTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectAliases_Alias",
                table: "ObjectAliases",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectAliases_ObjectName",
                table: "ObjectAliases",
                column: "ObjectName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ImageFileTag");

            migrationBuilder.DropTable(
                name: "ObjectAliases");

            migrationBuilder.DropTable(
                name: "ImageFiles");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Libraries");
        }
    }
}
