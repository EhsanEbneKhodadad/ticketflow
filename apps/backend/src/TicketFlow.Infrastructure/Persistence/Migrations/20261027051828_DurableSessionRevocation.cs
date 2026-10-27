using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableSessionRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_families",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_families", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_families_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_FamilyId",
                table: "refresh_sessions",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_session_families_UserId",
                table: "session_families",
                column: "UserId");

            // Legacy sessions predate durable family state. Preserve their rows for
            // diagnostics, but require a fresh sign-in after this security upgrade.
            migrationBuilder.Sql("""
                INSERT INTO session_families ("Id", "UserId", "SecurityStamp", "RevokedAt")
                SELECT DISTINCT r."FamilyId", r."UserId", COALESCE(u."SecurityStamp", ''), CURRENT_TIMESTAMP
                FROM refresh_sessions r
                JOIN "AspNetUsers" u ON u."Id" = r."UserId";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_sessions_session_families_FamilyId",
                table: "refresh_sessions",
                column: "FamilyId",
                principalTable: "session_families",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_sessions_session_families_FamilyId",
                table: "refresh_sessions");

            migrationBuilder.DropTable(
                name: "session_families");

            migrationBuilder.DropIndex(
                name: "IX_refresh_sessions_FamilyId",
                table: "refresh_sessions");
        }
    }
}
