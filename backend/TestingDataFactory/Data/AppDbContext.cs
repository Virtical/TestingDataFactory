using MassTransit;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Configuration;
using TestingDataFactory.Users;
using TestingDataFactory.Specifications;

namespace TestingDataFactory.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SpecificationJob> SpecificationJobs => Set<SpecificationJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseConfiguration.DefaultSchemaName);
        modelBuilder.AddTransactionalOutboxEntities();

        modelBuilder.Entity<SpecificationJob>(job =>
        {
            job.HasKey(j => j.JobId);
            job.Property(j => j.JobId).ValueGeneratedNever();
            job.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);
            job.HasOne<User>()
                .WithMany()
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.FirstName).HasMaxLength(100);
            user.Property(u => u.LastName).HasMaxLength(100);
            user.Property(u => u.Email).HasMaxLength(256);
        });
    }
}
