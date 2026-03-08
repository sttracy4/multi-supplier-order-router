using Microsoft.EntityFrameworkCore;
using OrderRouter.Api.Data;
using OrderRouter.Api.Models;
using OrderRouter.Api.Utilities;

namespace OrderRouter.Api.Services;

public class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public SupplierRepository(AppDbContext db) => _db = db;

    public async Task<List<Supplier>> GetEligibleSuppliersForAllCategoriesAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> requiredCategories)
    {
        var normalizedZip = ZipRangeParser.NormalizeZip(customerZip);
        var categories = requiredCategories.Select(c => c.ToLowerInvariant().Trim()).Distinct().ToList();

        // Start with ZIP eligibility filter (inline — EF Core can translate these)
        var query = ZipEligibleQuery(normalizedZip, mailOrderAllowed);

        // Chain one Where per required category — each becomes a translatable EXISTS subquery
        foreach (var cat in categories)
        {
            var captured = cat;
            query = query.Where(s => s.ProductCategories.Any(pc => pc.Category == captured));
        }

        return await query
            .Include(s => s.ServiceZips)
            .Include(s => s.ProductCategories)
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<Supplier>>> GetEligibleSuppliersByCategoryAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> categories)
    {
        var normalizedZip = ZipRangeParser.NormalizeZip(customerZip);
        var categoryList = categories.Select(c => c.ToLowerInvariant().Trim()).Distinct().ToList();

        // Load all ZIP-eligible suppliers who carry at least one of the needed categories
        var eligible = await ZipEligibleQuery(normalizedZip, mailOrderAllowed)
            .Where(s => s.ProductCategories.Any(pc => categoryList.Contains(pc.Category)))
            .Include(s => s.ServiceZips)
            .Include(s => s.ProductCategories)
            .ToListAsync();

        return categoryList.ToDictionary(
            cat => cat,
            cat => eligible.Where(s => s.ProductCategories.Any(pc => pc.Category == cat)).ToList());
    }

    private IQueryable<Supplier> ZipEligibleQuery(string normalizedZip, bool mailOrderAllowed) =>
        _db.Suppliers.Where(s =>
            s.ServesNationwide ||
            s.ServiceZips.Any(z => z.Zip == normalizedZip) ||
            (mailOrderAllowed && s.CanMailOrder));
}
