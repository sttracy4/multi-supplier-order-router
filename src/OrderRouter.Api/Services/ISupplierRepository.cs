using OrderRouter.Api.Models;

namespace OrderRouter.Api.Services;

public interface ISupplierRepository
{
    Task<List<Supplier>> GetEligibleSuppliersForAllCategoriesAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> requiredCategories);

    Task<Dictionary<string, List<Supplier>>> GetEligibleSuppliersByCategoryAsync(
        string customerZip, bool mailOrderAllowed, IEnumerable<string> categories);
}
