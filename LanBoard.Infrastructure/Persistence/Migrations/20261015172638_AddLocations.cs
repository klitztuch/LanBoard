using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Directions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Parking = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            // One location per distinct party location string, matched exactly without case folding.
            // Near-duplicates that differ only in case are cleaned up by hand afterwards.
            migrationBuilder.Sql("""
                INSERT INTO "Locations" ("Id", "Name")
                SELECT gen_random_uuid(), l."Location"
                FROM (SELECT DISTINCT "Location" FROM "LanParties") AS l;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "LanParties",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "LanParties" AS p
                SET "LocationId" = l."Id"
                FROM "Locations" AS l
                WHERE l."Name" = p."Location";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LocationId",
                table: "LanParties",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Location",
                table: "LanParties");

            migrationBuilder.CreateIndex(
                name: "IX_LanParties_LocationId",
                table: "LanParties",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name",
                table: "Locations",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LanParties_Locations_LocationId",
                table: "LanParties",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "LanParties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "LanParties" AS p
                SET "Location" = l."Name"
                FROM "Locations" AS l
                WHERE l."Id" = p."LocationId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "LanParties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_LanParties_Locations_LocationId",
                table: "LanParties");

            migrationBuilder.DropIndex(
                name: "IX_LanParties_LocationId",
                table: "LanParties");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "LanParties");

            migrationBuilder.DropTable(
                name: "Locations");
        }
    }
}
