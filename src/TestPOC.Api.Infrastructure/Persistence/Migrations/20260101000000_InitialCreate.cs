using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPOC.Api.Infrastructure.Persistence.Migrations;

// Hand-written initial migration so `Database.Migrate()` at runtime has something
// to apply without depending on the dotnet-ef design-time tool.
// If you later need to add follow-up migrations, install `dotnet-ef` globally
// and run `dotnet ef migrations remove` + `dotnet ef migrations add InitialCreate`
// to regenerate this file together with its Designer + AppDbContextModelSnapshot.
[DbContext(typeof(AppDbContext))]
[Migration("20260101000000_InitialCreate")]
public partial class InitialCreate : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Schema only. Seed data is loaded from
		// test/TestPOC.Api.Tests.Integration/TestInfrastructure/TestDatasets/items.csv
		// by SeedDatasetLoader.LoadItemsAsync (migration owns schema, tests own data).
		migrationBuilder.CreateTable(
			name: "items",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
				Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
				CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_items", x => x.Id);
			});
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "items");
	}
}
