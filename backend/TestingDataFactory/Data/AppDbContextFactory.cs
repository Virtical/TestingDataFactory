using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TestingDataFactory.Configuration;

namespace TestingDataFactory.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DatabaseConfiguration.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {DatabaseConfiguration.ConnectionStringEnvironmentVariable} for PostgreSQL.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.DefaultSchemaName))
            .Options;
        return new AppDbContext(options);
    }
}
