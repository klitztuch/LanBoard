using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Add nullable, backfill from DisplayName (= Steam name, no nicknames exist yet), then enforce NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "SteamName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Users" SET "SteamName" = "DisplayName";""");

            migrationBuilder.AlterColumn<string>(
                name: "SteamName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ProfileFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Labels = table.Column<string>(type: "jsonb", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Choices = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RealName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RealNameVisibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Clan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FavoriteGames = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    NotificationOptIns = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileFieldValues",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileFieldValues", x => new { x.UserId, x.FieldId });
                    table.ForeignKey(
                        name: "FK_ProfileFieldValues_ProfileFieldDefinitions_FieldId",
                        column: x => x.FieldId,
                        principalTable: "ProfileFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileFieldValues_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileFieldDefinitions_Key",
                table: "ProfileFieldDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileFieldValues_FieldId",
                table: "ProfileFieldValues",
                column: "FieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileFieldValues");

            migrationBuilder.DropTable(
                name: "ProfileFieldDefinitions");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SteamName",
                table: "Users");
        }
    }
}
