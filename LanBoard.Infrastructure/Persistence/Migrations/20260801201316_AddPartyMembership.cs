using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartyMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyMemberships_LanParties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "LanParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartyMemberships_PartyId",
                table: "PartyMemberships",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyMemberships_UserId_PartyId",
                table: "PartyMemberships",
                columns: new[] { "UserId", "PartyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyMemberships");
        }
    }
}
