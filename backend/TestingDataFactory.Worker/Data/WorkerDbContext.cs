using MassTransit;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Worker.Configuration;

namespace TestingDataFactory.Worker.Data;

public class WorkerDbContext(DbContextOptions<WorkerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseConfiguration.DefaultSchemaName);
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
