using OrderRouter.Api.Models;
using OrderRouter.Api.Utilities;

namespace OrderRouter.Api.Services;

public class SupplierRepository : ISupplierRepository
{
    private readonly ISupplierCache _cache;

    public SupplierRepository(ISupplierCache cache) => _cache = cache;

    public async Task<List<Supplier>> GetEligibleSuppliersForAllCategoriesAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> requiredCategories)
    {
        var categories = requiredCategories.Select(c => c.ToLowerInvariant().Trim()).Distinct().ToList();
        var all = await _cache.GetEligibleAsync(customerZip, mailOrderAllowed);
        return all
            .Where(s => categories.All(cat => s.ProductCategories.Any(pc => pc.Category == cat)))
            .ToList();
    }

    public async Task<Dictionary<string, List<Supplier>>> GetEligibleSuppliersByCategoryAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> categories)
    {
        var categoryList = categories.Select(c => c.ToLowerInvariant().Trim()).Distinct().ToList();
        var all = await _cache.GetEligibleAsync(customerZip, mailOrderAllowed);
        return categoryList.ToDictionary(
            cat => cat,
            cat => all.Where(s => s.ProductCategories.Any(pc => pc.Category == cat)).ToList());
    }
}
