using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomerSupport.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        var connectionString = args.Length > 0 
            ? args[0] 
            : "Server=localhost;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            sql.CommandTimeout(60);
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
