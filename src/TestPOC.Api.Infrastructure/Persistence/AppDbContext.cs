using Microsoft.EntityFrameworkCore;
using TestPOC.Api.Infrastructure.Persistence.Entities;

namespace TestPOC.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
	public DbSet<Item> Items => Set<Item>();

	public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		var items = modelBuilder.Entity<Item>();
		items.ToTable("items");
		items.HasKey(x => x.Id);
		items.Property(x => x.Name).HasMaxLength(100).IsRequired();
		items.Property(x => x.Price).HasPrecision(18, 2);
		items.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");

		var outbox = modelBuilder.Entity<OutboxMessage>();
		outbox.ToTable("outbox_messages");
		outbox.HasKey(x => x.Id);
		outbox.Property(x => x.Type).HasMaxLength(200).IsRequired();
		outbox.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
		outbox.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
		outbox.Property(x => x.SentAt).HasColumnType("timestamp with time zone");
		outbox.HasIndex(x => new { x.SentAt, x.CreatedAt })
			.HasDatabaseName("ix_outbox_messages_pending")
			.HasFilter("\"SentAt\" IS NULL");
	}
}
