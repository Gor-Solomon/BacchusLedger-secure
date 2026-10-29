using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BacchusLedger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    vessel = table.Column<string>(type: "TEXT", nullable: true),
                    seq = table.Column<long>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "pending"),
                    attempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_error = table.Column<string>(type: "TEXT", nullable: true),
                    processed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.CheckConstraint("CK_outbox_messages_status", "status IN ('pending','processed','failed')");
                });

            migrationBuilder.CreateTable(
                name: "processed_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    handler = table.Column<string>(type: "TEXT", nullable: false),
                    processed_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_messages", x => new { x.message_id, x.handler });
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_seq",
                table: "outbox_messages",
                column: "seq",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_next_attempt_at",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "processed_messages");
        }
    }
}
