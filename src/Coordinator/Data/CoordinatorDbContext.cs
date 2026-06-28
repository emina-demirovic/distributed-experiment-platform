using Microsoft.EntityFrameworkCore;

namespace Coordinator.Data;

public sealed class CoordinatorDbContext(
    DbContextOptions<CoordinatorDbContext> options)
    : DbContext(options)
{
    public DbSet<ExperimentEntity> Experiments =>
        Set<ExperimentEntity>();

    public DbSet<ExperimentEventEntity> ExperimentEvents =>
        Set<ExperimentEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExperimentEntity>(entity =>
        {
            entity.HasKey(experiment => experiment.Id);

            entity.Property(experiment => experiment.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(experiment => experiment.Algorithm)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(experiment => experiment.Environment)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(experiment => experiment.TimeoutSeconds)
                .HasDefaultValue(300);
        });

        modelBuilder.Entity<ExperimentEventEntity>(entity =>
        {
            entity.HasKey(experimentEvent => experimentEvent.Id);

            entity.Property(experimentEvent => experimentEvent.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(experimentEvent => experimentEvent.Details)
                .HasMaxLength(1000);

            entity.HasOne(experimentEvent => experimentEvent.Experiment)
                .WithMany()
                .HasForeignKey(experimentEvent => experimentEvent.ExperimentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}