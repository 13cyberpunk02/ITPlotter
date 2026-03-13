using ITPlotter.Application.Interfaces;
using ITPlotter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITPlotter.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalFileName).HasMaxLength(500);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CupsName).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.CupsName).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(300);
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Document)
                .WithMany(d => d.PrintJobs)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Printer)
                .WithMany(p => p.PrintJobs)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
