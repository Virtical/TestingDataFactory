using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TestingDataFactory.Worker.Configuration;

namespace TestingDataFactory.Worker.Data;

// Миграции Worker-а не требуют запуска шины или наличия consumers.
public class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DatabaseConfiguration.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {DatabaseConfiguration.ConnectionStringEnvironmentVariable} for PostgreSQL.");

        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.DefaultSchemaName))
            .Options;
        return new WorkerDbContext(options);
    }
}
