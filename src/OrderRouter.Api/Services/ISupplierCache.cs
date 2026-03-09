using OrderRouter.Api.Models;

namespace OrderRouter.Api.Services;

public interface ISupplierCache
{
    Task<IReadOnlyList<Supplier>> GetEligibleAsync(string customerZip, bool mailOrderAllowed);
}
