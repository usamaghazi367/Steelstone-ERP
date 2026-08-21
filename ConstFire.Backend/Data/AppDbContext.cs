using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ErpModule> ErpModules => Set<ErpModule>();
    public DbSet<ErpModuleField> ErpModuleFields => Set<ErpModuleField>();
    public DbSet<ErpRecord> ErpRecords => Set<ErpRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.Name).HasMaxLength(128);
            entity.Property(u => u.Role).HasMaxLength(64);
        });

        modelBuilder.Entity<ErpModule>(entity =>
        {
            entity.HasIndex(m => m.Code).IsUnique();
            entity.Property(m => m.Code).HasMaxLength(8);
            entity.Property(m => m.Name).HasMaxLength(256);
            entity.Property(m => m.Category).HasMaxLength(128);
        });

        modelBuilder.Entity<ErpModuleField>(entity =>
        {
            entity.HasIndex(f => new { f.ModuleId, f.Ref }).IsUnique();
            entity.Property(f => f.Ref).HasMaxLength(16);
            entity.Property(f => f.FieldName).HasMaxLength(512);
            entity.Property(f => f.DataType).HasMaxLength(128);
            entity.Property(f => f.Mandatory).HasMaxLength(32);
            entity.HasOne(f => f.Module).WithMany(m => m.Fields).HasForeignKey(f => f.ModuleId);
        });

        modelBuilder.Entity<ErpRecord>(entity =>
        {
            entity.Property(r => r.DataJson).HasColumnType("nvarchar(max)");
            entity.Property(r => r.RecordCode).HasMaxLength(32);
            entity.HasIndex(r => new { r.ModuleId, r.RecordCode }).IsUnique()
                .HasFilter("[RecordCode] IS NOT NULL");
            entity.HasOne(r => r.Module).WithMany(m => m.Records).HasForeignKey(r => r.ModuleId);
        });
    }
}
