using JobScheduler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScheduler.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.Payload)
            .IsRequired();

        // Store the enum as readable text ("Pending") instead of a number
        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.LastError)
            .HasMaxLength(2000);

        // The worker's query: Status = Pending AND NextRunTime is due
        builder.HasIndex(j => new { j.Status, j.NextRunTime });
    }
}