using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPOC.Api.Infrastructure.Persistence.Migrations;

// Hand-written follow-up migration for the outbox pattern.
// Kept in sync with AppDbContext.OnModelCreating for OutboxMessage.
[DbContext(typeof(AppDbContext))]
[Migration("20260101000001_AddOutboxMessages")]
public partial class AddOutboxMessages : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "outbox_messages",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
				Payload = table.Column<string>(type: "jsonb", nullable: false),
				CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
				Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_outbox_messages", x => x.Id);
			});

		migrationBuilder.CreateIndex(
			name: "ix_outbox_messages_pending",
			table: "outbox_messages",
			columns: new[] { "SentAt", "CreatedAt" },
			filter: "\"SentAt\" IS NULL");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "outbox_messages");
	}
}
