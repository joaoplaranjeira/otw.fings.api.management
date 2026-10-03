using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace otw.fings.api.management.Infrastructure.Data;

public sealed class FingsDbContextFactory : IDesignTimeDbContextFactory<FingsDbContext>
{
    public FingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "server=localhost;database=fings;user=fings;password=development";
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new FingsDbContext(options);
    }
}
