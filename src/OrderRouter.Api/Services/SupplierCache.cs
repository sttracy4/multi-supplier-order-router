using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using OrderRouter.Api.Data;
using OrderRouter.Api.Models;
using OrderRouter.Api.Utilities;

namespace OrderRouter.Api.Services;

public class SupplierCache : ISupplierCache
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<Supplier>> _store = new();

    public SupplierCache(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<Supplier>> GetEligibleAsync(string customerZip, bool mailOrderAllowed)
    {
        var zip = ZipRangeParser.NormalizeZip(customerZip);
        var key = $"{zip}:{mailOrderAllowed}";

        if (_store.TryGetValue(key, out var cached)) return cached;

        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_store.TryGetValue(key, out cached)) return cached;

            var loaded = await LoadAsync(zip, mailOrderAllowed);
            _store[key] = loaded;
            return loaded;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<IReadOnlyList<Supplier>> LoadAsync(string normalizedZip, bool mailOrderAllowed)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Suppliers
            .Where(s =>
                s.ServesNationwide ||
                s.ServiceZips.Any(z => z.Zip == normalizedZip) ||
                (mailOrderAllowed && s.CanMailOrder))
            .Include(s => s.ProductCategories)
            .AsNoTracking();

        // Filtered include is only supported on relational providers (not InMemory)
        query = db.Database.IsRelational()
            ? query.Include(s => s.ServiceZips.Where(z => z.Zip == normalizedZip))
            : query.Include(s => s.ServiceZips);

        return await query.ToListAsync();
    }
}
