using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInAppNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "InAppOptOuts",
                table: "UserProfiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            // Hand-edited: existing rows get sound on (the model has no default, see UserProfileConfiguration).
            migrationBuilder.AddColumn<bool>(
                name: "NotificationSoundEnabled",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InAppOptOuts",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "NotificationSoundEnabled",
                table: "UserProfiles");
        }
    }
}
