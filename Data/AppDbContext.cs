using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Flyer> Flyers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>()
            .HasOne(u => u.Company)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.CompanyId);

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion<string>();

        // Company configuration
        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<Company>()
            .Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        modelBuilder.Entity<Company>()
            .Property(c => c.ContactEmail)
            .HasMaxLength(255);

        // Flyer configuration
        modelBuilder.Entity<Flyer>()
            .HasOne(f => f.Company)
            .WithMany(c => c.Flyers)
            .HasForeignKey(f => f.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Flyer>()
            .HasIndex(f => new { f.CompanyId, f.ForDate });

        modelBuilder.Entity<Flyer>()
            .HasIndex(f => f.ForDate);

        modelBuilder.Entity<Flyer>()
            .Property(f => f.Title)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<Flyer>()
            .Property(f => f.ImagePath)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<Flyer>()
            .Property(f => f.ForDate)
            .HasColumnType("date")
            .IsRequired();

        // Global query filters
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => u.IsActive);

        modelBuilder.Entity<Company>()
            .HasQueryFilter(c => c.IsActive);

        modelBuilder.Entity<Flyer>()
            .HasQueryFilter(f => !f.IsDeleted);
    }
}
