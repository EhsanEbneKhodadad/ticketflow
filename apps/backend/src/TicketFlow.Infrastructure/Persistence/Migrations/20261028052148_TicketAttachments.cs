using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ticket_messages_Id_TicketId",
                table: "ticket_messages",
                columns: new[] { "Id", "TicketId" });

            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Internal = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_attachments", x => x.Id);
                    table.CheckConstraint("CK_attachment_size", "\"Size\" > 0 AND \"Size\" <= 10485760");
                    table.ForeignKey(
                        name: "FK_ticket_attachments_ticket_messages_MessageId_TicketId",
                        columns: x => new { x.MessageId, x.TicketId },
                        principalTable: "ticket_messages",
                        principalColumns: new[] { "Id", "TicketId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_attachments_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_MessageId_TicketId",
                table: "ticket_attachments",
                columns: new[] { "MessageId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_ObjectKey",
                table: "ticket_attachments",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_TicketId_CreatedAt",
                table: "ticket_attachments",
                columns: new[] { "TicketId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_attachments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ticket_messages_Id_TicketId",
                table: "ticket_messages");
        }
    }
}
