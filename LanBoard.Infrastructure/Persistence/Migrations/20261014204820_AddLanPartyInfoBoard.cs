using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanPartyInfoBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InfoBoard",
                table: "LanParties",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InfoBoard",
                table: "LanParties");
        }
    }
}
