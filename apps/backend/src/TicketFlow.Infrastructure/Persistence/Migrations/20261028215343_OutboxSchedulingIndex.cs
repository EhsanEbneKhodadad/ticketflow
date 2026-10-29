using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxSchedulingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_TicketId_TicketVersion_Kind",
                table: "outbox_messages",
                columns: new[] { "TicketId", "TicketVersion", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_TicketId_TicketVersion_Kind",
                table: "outbox_messages");
        }
    }
}
