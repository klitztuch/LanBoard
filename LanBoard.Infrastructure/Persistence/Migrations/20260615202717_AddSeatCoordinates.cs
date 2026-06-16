using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "X",
                table: "Seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Y",
                table: "Seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "X",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Y",
                table: "Seats");
        }
    }
}
