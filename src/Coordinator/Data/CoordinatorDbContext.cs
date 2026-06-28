using Microsoft.EntityFrameworkCore;

namespace Coordinator.Data;

public sealed class CoordinatorDbContext(
    DbContextOptions<CoordinatorDbContext> options)
    : DbContext(options)
{
    public DbSet<ExperimentEntity> Experiments =>
        Set<ExperimentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExperimentEntity>(entity =>
        {
            entity.HasKey(experiment => experiment.Id);

            entity.Property(experiment => experiment.Name)
                .IsRequired()
                .HasMaxLength(200);
        });
    }
}