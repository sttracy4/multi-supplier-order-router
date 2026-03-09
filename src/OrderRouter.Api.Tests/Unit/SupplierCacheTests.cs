using Microsoft.EntityFrameworkCore;
using Moq;
using OrderRouter.Api.Data;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;
using Xunit;

namespace OrderRouter.Api.Tests.Unit;

public class SupplierCacheTests
{
    private static AppDbContext BuildDb(IEnumerable<Supplier> suppliers)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.Suppliers.AddRange(suppliers);
        db.SaveChanges();
        return db;
    }

    private static Supplier MakeSupplier(int id, string zip, string category) => new()
    {
        Id = id,
        SupplierId = $"SUP-{id:D3}",
        SupplierName = $"Supplier {id}",
        SatisfactionScore = 8m,
        ServesNationwide = false,
        CanMailOrder = false,
        ServiceZips = new List<SupplierServiceZip> { new() { Zip = zip } },
        ProductCategories = new List<SupplierProductCategory> { new() { Category = category } },
    };

    private static IDbContextFactory<AppDbContext> FactoryFor(params Supplier[] suppliers)
    {
        var mock = new Mock<IDbContextFactory<AppDbContext>>();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildDb(suppliers));
        return mock.Object;
    }

    [Fact]
    public async Task GetEligibleAsync_ColdMiss_ReturnsMatchingSuppliers()
    {
        var supplier = MakeSupplier(1, "10001", "widgets");
        var cache = new SupplierCache(FactoryFor(supplier));

        var result = await cache.GetEligibleAsync("10001", false);

        Assert.Single(result);
        Assert.Equal("SUP-001", result[0].SupplierId);
    }

    [Fact]
    public async Task GetEligibleAsync_WarmHit_DbCalledOnlyOnce()
    {
        var supplier = MakeSupplier(1, "10001", "widgets");
        var callCount = 0;

        var mock = new Mock<IDbContextFactory<AppDbContext>>();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return BuildDb(new[] { supplier });
            });

        var cache = new SupplierCache(mock.Object);

        await cache.GetEligibleAsync("10001", false);
        await cache.GetEligibleAsync("10001", false);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetEligibleAsync_DifferentKeys_DbCalledPerKey()
    {
        var callCount = 0;

        var mock = new Mock<IDbContextFactory<AppDbContext>>();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return BuildDb(Array.Empty<Supplier>());
            });

        var cache = new SupplierCache(mock.Object);

        await cache.GetEligibleAsync("10001", false);
        await cache.GetEligibleAsync("10002", false); // different ZIP
        await cache.GetEligibleAsync("10001", true);  // same ZIP, different mailOrder

        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task GetEligibleAsync_ConcurrentCallsSameKey_DbLoadedOnce()
    {
        var callCount = 0;
        var tcs = new TaskCompletionSource();

        var mock = new Mock<IDbContextFactory<AppDbContext>>();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await tcs.Task; // hold until released
                return BuildDb(Array.Empty<Supplier>());
            });

        var cache = new SupplierCache(mock.Object);

        // Fire two concurrent requests for the same key
        var t1 = cache.GetEligibleAsync("10001", false);
        var t2 = cache.GetEligibleAsync("10001", false);

        // Give both tasks time to start; t2 should now be blocked on the semaphore
        await Task.Delay(50);

        tcs.SetResult(); // unblock the DB call
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetEligibleAsync_MailOrderFalse_ExcludesMailOrderOnlySupplier()
    {
        var mailOnlySupplier = new Supplier
        {
            Id = 1, SupplierId = "SUP-001", SupplierName = "Mail Only", SatisfactionScore = 8m,
            ServesNationwide = false, CanMailOrder = true,
            ServiceZips = new List<SupplierServiceZip>(),
            ProductCategories = new List<SupplierProductCategory> { new() { Category = "widgets" } },
        };

        var cache = new SupplierCache(FactoryFor(mailOnlySupplier));

        var result = await cache.GetEligibleAsync("99999", false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEligibleAsync_MailOrderTrue_IncludesMailOrderOnlySupplier()
    {
        var mailOnlySupplier = new Supplier
        {
            Id = 1, SupplierId = "SUP-001", SupplierName = "Mail Only", SatisfactionScore = 8m,
            ServesNationwide = false, CanMailOrder = true,
            ServiceZips = new List<SupplierServiceZip>(),
            ProductCategories = new List<SupplierProductCategory> { new() { Category = "widgets" } },
        };

        var cache = new SupplierCache(FactoryFor(mailOnlySupplier));

        var result = await cache.GetEligibleAsync("99999", true);

        Assert.Single(result);
    }
}
