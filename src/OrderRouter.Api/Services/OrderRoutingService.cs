using Microsoft.EntityFrameworkCore;
using OrderRouter.Api.Data;
using OrderRouter.Api.DTOs;
using OrderRouter.Api.Models;
using OrderRouter.Api.Utilities;

namespace OrderRouter.Api.Services;

public class OrderRoutingService : IOrderRoutingService
{
    private readonly AppDbContext _db;
    private readonly ISupplierRepository _supplierRepo;

    public OrderRoutingService(AppDbContext db, ISupplierRepository supplierRepo)
    {
        _db = db;
        _supplierRepo = supplierRepo;
    }

    private record ItemWithCategory(string ProductCode, int Quantity, string Category);

    public async Task<OrderResponse> RouteAsync(OrderRequest request)
    {
        // Step 1: Resolve product categories (case-insensitive lookup, collect all unknowns)
        var mergedItems = request.Items
            .GroupBy(i => i.ProductCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new OrderItem { ProductCode = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        var itemCategories = new List<ItemWithCategory>();
        var unknownCodes = new List<string>();
        foreach (var item in mergedItems)
        {
            var lowerCode = item.ProductCode.ToLowerInvariant();
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductCode.ToLower() == lowerCode);

            if (product == null)
                unknownCodes.Add(item.ProductCode);
            else
                itemCategories.Add(new ItemWithCategory(product.ProductCode, item.Quantity, product.Category));
        }

        if (unknownCodes.Count > 0)
            return Infeasible(unknownCodes.Select(c => $"Unknown product code: {c}"));

        var requiredCategories = itemCategories.Select(x => x.Category).Distinct().ToList();
        var normalizedZip = ZipRangeParser.NormalizeZip(request.CustomerZip);

        // Step 2: Attempt single-supplier consolidation
        var consolidatedCandidates = await _supplierRepo
            .GetEligibleSuppliersForAllCategoriesAsync(request.CustomerZip, request.MailOrder, requiredCategories);

        if (consolidatedCandidates.Count > 0)
        {
            var best = SelectBest(consolidatedCandidates, normalizedZip);
            return BuildResponse(new Dictionary<Supplier, List<ItemWithCategory>>
            {
                [best] = itemCategories
            }, normalizedZip);
        }

        // Step 3: Fallback — route per category
        var byCategory = await _supplierRepo
            .GetEligibleSuppliersByCategoryAsync(request.CustomerZip, request.MailOrder, requiredCategories);

        var missingCategories = requiredCategories
            .Where(cat => !byCategory.TryGetValue(cat, out var c) || c.Count == 0)
            .ToList();

        if (missingCategories.Count > 0)
            return Infeasible(missingCategories.Select(cat => $"No eligible supplier found for category: {cat}"));

        // Assign best supplier per category
        var assignedByCategory = requiredCategories.ToDictionary(
            cat => cat,
            cat => SelectBest(byCategory[cat], normalizedZip));

        // Group items by assigned supplier
        var supplierGroups = new Dictionary<int, (Supplier Supplier, List<ItemWithCategory> Items)>();
        foreach (var iwc in itemCategories)
        {
            var supplier = assignedByCategory[iwc.Category];
            if (!supplierGroups.TryGetValue(supplier.Id, out var group))
            {
                group = (supplier, new List<ItemWithCategory>());
                supplierGroups[supplier.Id] = group;
            }
            group.Items.Add(iwc);
        }

        var grouped = supplierGroups.Values.ToDictionary(g => g.Supplier, g => g.Items);
        return BuildResponse(grouped, normalizedZip);
    }

    private static Supplier SelectBest(IEnumerable<Supplier> candidates, string normalizedZip) =>
        candidates
            .OrderByDescending(s => s.SatisfactionScore)
            .ThenByDescending(s => IsLocal(s, normalizedZip) ? 1 : 0)
            .First();

    private static bool IsLocal(Supplier s, string normalizedZip) =>
        s.ServesNationwide || s.ServiceZips.Any(z => z.Zip == normalizedZip);

    private static string DetermineFulfillmentMode(Supplier supplier, string normalizedZip) =>
        IsLocal(supplier, normalizedZip) ? "local" : "mail_order";

    private static OrderResponse BuildResponse(
        Dictionary<Supplier, List<ItemWithCategory>> groups,
        string normalizedZip)
    {
        var routing = groups.Select(kvp => new RoutedSupplier
        {
            SupplierId = kvp.Key.SupplierId,
            SupplierName = kvp.Key.SupplierName,
            Items = kvp.Value.Select(x => new RoutedItem
            {
                ProductCode = x.ProductCode,
                Quantity = x.Quantity,
                Category = x.Category,
                FulfillmentMode = DetermineFulfillmentMode(kvp.Key, normalizedZip),
            }).ToList(),
        }).ToList();

        return new OrderResponse { Feasible = true, Routing = routing };
    }

    private static OrderResponse Infeasible(string error) =>
        new() { Feasible = false, Errors = new List<string> { error } };

    private static OrderResponse Infeasible(IEnumerable<string> errors) =>
        new() { Feasible = false, Errors = errors.ToList() };
}
