using Microsoft.EntityFrameworkCore;
using OrderRouter.Api.Models;

namespace OrderRouter.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierServiceZip> SupplierServiceZips => Set<SupplierServiceZip>();
    public DbSet<SupplierProductCategory> SupplierProductCategories => Set<SupplierProductCategory>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(e =>
        {
            e.HasIndex(s => s.SupplierId).IsUnique();
            e.HasMany(s => s.ServiceZips)
             .WithOne(z => z.Supplier)
             .HasForeignKey(z => z.SupplierId);
            e.HasMany(s => s.ProductCategories)
             .WithOne(c => c.Supplier)
             .HasForeignKey(c => c.SupplierId);
        });

        modelBuilder.Entity<SupplierServiceZip>(e =>
        {
            e.HasIndex(z => z.Zip);
        });

        modelBuilder.Entity<SupplierProductCategory>(e =>
        {
            e.HasIndex(c => c.Category);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.ProductCode).IsUnique();
        });
    }
}
