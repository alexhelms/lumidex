using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumidex.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RaDecRTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the rtree table.
            migrationBuilder.Sql(
                sql: """
                CREATE VIRTUAL TABLE RaDecRTree USING rtree(
                    Id,
                    MinRa, MaxRa,
                    MinDec, MaxDec
                );

                CREATE TRIGGER IF NOT EXISTS AddRaDecToRTreeAfterInsert
                	AFTER INSERT
                	ON ImageFiles
                	FOR EACH ROW
                	WHEN 1 = 1
                		AND NEW.RightAscension IS NOT NULL
                		AND NEW.Declination IS NOT NULL
                BEGIN
                	INSERT INTO RaDecRTree
                	VALUES (NEW.Id, NEW.RightAscension, NEW.RightAscension, NEW.Declination, NEW.Declination);
                END;

                CREATE TRIGGER IF NOT EXISTS UpdateRaDecToRTreeAfterUpdate
                	AFTER UPDATE
                	ON ImageFiles
                	FOR EACH ROW
                	WHEN 1 = 1
                        AND OLD.Id = NEW.Id
                		AND NEW.RightAscension IS NOT NULL
                		AND NEW.Declination IS NOT NULL
                BEGIN
                	UPDATE RaDecRTree
                	SET MinRa = NEW.RightAscension,
                		MaxRa = NEW.RightAscension,
                		MinDec = NEW.Declination,
                		MaxDec = NEW.Declination
                	WHERE Id = NEW.Id;
                END;

                CREATE TRIGGER IF NOT EXISTS DeleteRaDecToRTreeAfterDelete
                	AFTER DELETE
                	ON ImageFiles
                	FOR EACH ROW
                BEGIN
                	DELETE FROM RaDecRTree
                	WHERE Id = OLD.Id;
                END;

                """);

            // Populate RaDecRTree from existing data.
            migrationBuilder.Sql(
                sql: """
                INSERT INTO RaDecRTree
                SELECT Id, RightAscension as MinRa, RightAscension as MaxRa, Declination as MinDec, Declination as MaxDec
                FROM (
                	SELECT Id, RightAscension, Declination
                	FROM ImageFiles
                	WHERE 1 = 1
                		AND RightAscension IS NOT NULL
                		AND Declination IS NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaDecRTree");

            migrationBuilder.Sql(
                sql: """
                DROP TRIGGER AddRaDecToRTreeAfterInsert;
                DROP TRIGGER UpdateRaDecToRTreeAfterUpdate;
                DROP TRIGGER DeleteRaDecToRTreeAfterDelete;
                """);
        }
    }
}
