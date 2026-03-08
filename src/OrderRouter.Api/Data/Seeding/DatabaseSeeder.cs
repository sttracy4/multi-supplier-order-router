using Microsoft.EntityFrameworkCore;

namespace OrderRouter.Api.Data.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, string resourcesPath, ILogger logger)
    {
        if (await db.Suppliers.AnyAsync())
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding database from {Path}...", resourcesPath);

        var suppliersFile = Path.Combine(resourcesPath, "suppliers.csv");
        var productsFile = Path.Combine(resourcesPath, "products.csv");

        if (!File.Exists(suppliersFile))
            throw new FileNotFoundException($"suppliers.csv not found at {suppliersFile}");
        if (!File.Exists(productsFile))
            throw new FileNotFoundException($"products.csv not found at {productsFile}");

        var suppliers = SupplierCsvParser.Parse(suppliersFile, logger);
        db.Suppliers.AddRange(suppliers);

        var products = ProductCsvParser.Parse(productsFile, logger);
        db.Products.AddRange(products);

        await db.SaveChangesAsync();

        logger.LogInformation("Seeding complete: {SupplierCount} suppliers, {ProductCount} products.",
            suppliers.Count, products.Count);
    }
}
