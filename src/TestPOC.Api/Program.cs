using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using TestPOC.Api.GraphQl;
using TestPOC.Api.Infrastructure.Messaging;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
	?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
	?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
	?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
	?? throw new InvalidOperationException("Jwt:Audience is not configured.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
	?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConnection>(_ =>
{
	var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
	return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddScoped<IEventPublisher, OutboxEventPublisher>();
builder.Services.AddScoped<IItemsService, ItemsService>();
builder.Services.AddHostedService<OutboxProcessor>();

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = jwtIssuer,
			ValidAudience = jwtAudience,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
			ClockSkew = TimeSpan.Zero,
		};
	});
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
	.AddGraphQLServer()
	.AddAuthorization()
	.AddQueryType<Query>()
	.AddMutationType<Mutation>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGraphQL();

await app.RunAsync();

public partial class Program
{
    protected Program()
    {
    }
}
