using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using CustomerSupport.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();

var config = host.Services.GetRequiredService<IConfiguration>();
var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

Log.Information("Running migrations for CustomerSupport Platform database...");
await RunMigrationAsync(connectionString);
Log.Information("All migrations completed successfully.");

static async Task RunMigrationAsync(string connectionString)
{
    Log.Information("Running migrations...");
    var services = new ServiceCollection();
    services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connectionString));
    await using var sp = services.BuildServiceProvider();
    await using var scope = sp.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Migrations completed successfully.");
}
