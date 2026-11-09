using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<HoldEntity> Holds => Set<HoldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassEntity>(e =>
        {
            e.ToTable("classes", t =>
            {
                t.HasCheckConstraint("ck_classes_total_seats", "total_seats BETWEEN 1 AND 100000");
                t.HasCheckConstraint("ck_classes_name_length", "char_length(btrim(name)) BETWEEN 1 AND 100");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.TotalSeats).HasColumnName("total_seats");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<HoldEntity>(e =>
        {
            var statuses = string.Join(",", HoldStatus.All.Select(s => $"'{s}'"));
            e.ToTable("holds", t =>
            {
                t.HasCheckConstraint("ck_holds_status", $"status IN ({statuses})");
                t.HasCheckConstraint("ck_holds_student_id", "student_id ~ '^[0-9]{17}$'");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClassId).HasColumnName("class_id");
            e.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            e.Property(x => x.ExpiredAt).HasColumnName("expired_at");

            e.HasOne<ClassEntity>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);

            // Seat counting per class.
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt }).HasDatabaseName("ix_holds_class_status_expires");
            // "My applications" lookups.
            e.HasIndex(x => new { x.StudentId, x.CreatedAt }).IsDescending(false, true)
                .HasDatabaseName("ix_holds_student_created");
            // Safety net for "one active application or confirmed admission per student per class".
            e.HasIndex(x => new { x.ClassId, x.StudentId }).IsUnique()
                .HasFilter("status IN ('HELD','CONFIRMED')")
                .HasDatabaseName("ux_holds_active_student_class");
        });
    }
}
