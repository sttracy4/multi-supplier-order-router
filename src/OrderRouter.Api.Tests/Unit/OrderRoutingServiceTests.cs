using Microsoft.EntityFrameworkCore;
using Moq;
using OrderRouter.Api.Data;
using OrderRouter.Api.DTOs;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;
using Xunit;

namespace OrderRouter.Api.Tests.Unit;

public class OrderRoutingServiceTests
{
    private static AppDbContext BuildDb(params Product[] products)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.Products.AddRange(products);
        db.SaveChanges();
        return db;
    }

    private static Supplier LocalSupplier(string id, string name, string zip, string category, decimal score) =>
        new()
        {
            Id = int.Parse(id.Replace("SUP-", "")),
            SupplierId = id,
            SupplierName = name,
            SatisfactionScore = score,
            CanMailOrder = false,
            ServesNationwide = false,
            ServiceZips = new List<SupplierServiceZip> { new() { Zip = zip } },
            ProductCategories = new List<SupplierProductCategory> { new() { Category = category } },
        };

    private static Supplier MailOrderSupplier(string id, string name, string category, decimal score) =>
        new()
        {
            Id = int.Parse(id.Replace("SUP-", "")),
            SupplierId = id,
            SupplierName = name,
            SatisfactionScore = score,
            CanMailOrder = true,
            ServesNationwide = false,
            ServiceZips = new List<SupplierServiceZip>(),
            ProductCategories = new List<SupplierProductCategory> { new() { Category = category } },
        };

    private static OrderRequest SimpleRequest(string zip, bool mailOrder, params string[] productCodes) =>
        new()
        {
            OrderId = "ORD-TEST",
            CustomerZip = zip,
            MailOrder = mailOrder,
            Items = productCodes.Select(pc => new OrderItem { ProductCode = pc, Quantity = 1 }).ToList(),
        };

    // Scenario 1: Single supplier covers all items
    [Fact]
    public async Task Route_SingleSupplierCoversAll_ReturnsSingleEntry()
    {
        var db = BuildDb(
            new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" },
            new Product { Id = 2, ProductCode = "B001", ProductName = "Widget B", Category = "gadgets" });

        var supplier = new Supplier
        {
            Id = 1, SupplierId = "SUP-001", SupplierName = "Acme", SatisfactionScore = 9,
            CanMailOrder = false, ServesNationwide = false,
            ServiceZips = new List<SupplierServiceZip> { new() { Zip = "10001" } },
            ProductCategories = new List<SupplierProductCategory>
            {
                new() { Category = "widgets" }, new() { Category = "gadgets" }
            },
        };

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("10001", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { supplier });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", false, "A001", "B001"));

        Assert.True(result.Feasible);
        Assert.Single(result.Routing);
        Assert.Equal("SUP-001", result.Routing[0].SupplierId);
        Assert.Equal(2, result.Routing[0].Items.Count);
        Assert.All(result.Routing[0].Items, item => Assert.Equal("local", item.FulfillmentMode));
    }

    // Scenario 2: Multi-supplier fallback
    [Fact]
    public async Task Route_NoSingleSupplierCoversAll_ReturnsTwoSuppliers()
    {
        var db = BuildDb(
            new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" },
            new Product { Id = 2, ProductCode = "B001", ProductName = "Gadget B", Category = "gadgets" });

        var s1 = LocalSupplier("SUP-001", "Acme Widgets", "10001", "widgets", 9m);
        var s2 = LocalSupplier("SUP-002", "Gadget Shop", "10001", "gadgets", 7m);

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier>());
        repo.Setup(r => r.GetEligibleSuppliersByCategoryAsync("10001", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, List<Supplier>>
            {
                ["widgets"] = new List<Supplier> { s1 },
                ["gadgets"] = new List<Supplier> { s2 },
            });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", false, "A001", "B001"));

        Assert.True(result.Feasible);
        Assert.Equal(2, result.Routing.Count);
    }

    // Scenario 3: No supplier for a category → infeasible
    [Fact]
    public async Task Route_NoSupplierForCategory_ReturnsFalse()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "X001", ProductName = "Rare", Category = "rarecat" });

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier>());
        repo.Setup(r => r.GetEligibleSuppliersByCategoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, List<Supplier>> { ["rarecat"] = new List<Supplier>() });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", false, "X001"));

        Assert.False(result.Feasible);
        Assert.Contains(result.Errors!, e => e.Contains("rarecat"));
    }

    // Scenario 4: mail_order=false excludes mail-order-only supplier
    [Fact]
    public async Task Route_MailOrderFalse_ExcludesMailOrderOnlySupplier()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var repo = new Mock<ISupplierRepository>();
        // Repo returns empty — simulates that the ZIP-only filter excluded the mail-order supplier
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("99999", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier>());
        repo.Setup(r => r.GetEligibleSuppliersByCategoryAsync("99999", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, List<Supplier>> { ["widgets"] = new List<Supplier>() });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("99999", false, "A001"));

        Assert.False(result.Feasible);
    }

    // Scenario 5: mail_order=true includes mail-order supplier with mode "mail_order"
    [Fact]
    public async Task Route_MailOrderTrue_IncludesMailOrderSupplier_WithCorrectMode()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var mailSupplier = MailOrderSupplier("SUP-010", "National Supply", "widgets", 7m);

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("99999", true, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { mailSupplier });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("99999", true, "A001"));

        Assert.True(result.Feasible);
        Assert.Single(result.Routing);
        Assert.Equal("mail_order", result.Routing[0].Items[0].FulfillmentMode);
    }

    // Scenario 6: Score tie → local supplier wins
    [Fact]
    public async Task Route_TiedScore_LocalWinsOverMailOrder()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var localSupplier = LocalSupplier("SUP-001", "Local Co", "10001", "widgets", 8m);
        var mailSupplier = MailOrderSupplier("SUP-002", "National Co", "widgets", 8m);

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("10001", true, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { mailSupplier, localSupplier }); // mail-order first to test ordering

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", true, "A001"));

        Assert.True(result.Feasible);
        Assert.Equal("SUP-001", result.Routing[0].SupplierId); // local wins
        Assert.Equal("local", result.Routing[0].Items[0].FulfillmentMode);
    }

    // Scenario 7: "no ratings yet" (score=0) loses to any rated supplier
    [Fact]
    public async Task Route_UnratedSupplierLosesToRated()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var ratedSupplier = LocalSupplier("SUP-001", "Rated Co", "10001", "widgets", 6m);
        var unratedSupplier = LocalSupplier("SUP-002", "Unrated Co", "10001", "widgets", 0m);

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("10001", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { unratedSupplier, ratedSupplier }); // unrated first

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", false, "A001"));

        Assert.True(result.Feasible);
        Assert.Equal("SUP-001", result.Routing[0].SupplierId); // rated wins
    }

    // Scenario 8: Unknown product code → infeasible
    [Fact]
    public async Task Route_UnknownProductCode_ReturnsFalse()
    {
        var db = BuildDb(); // no products seeded

        var repo = new Mock<ISupplierRepository>();
        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("10001", false, "DOES-NOT-EXIST"));

        Assert.False(result.Feasible);
        Assert.Contains(result.Errors!, e => e.Contains("DOES-NOT-EXIST"));
    }

    // Scenario 9: Duplicate product codes in same order → both line items preserved
    [Fact]
    public async Task Route_DuplicateProductCodesInItems_ReturnsBothLineItems()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var supplier = new Supplier
        {
            Id = 1, SupplierId = "SUP-001", SupplierName = "Acme", SatisfactionScore = 9,
            CanMailOrder = false, ServesNationwide = false,
            ServiceZips = new List<SupplierServiceZip> { new() { Zip = "10001" } },
            ProductCategories = new List<SupplierProductCategory> { new() { Category = "widgets" } },
        };

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("10001", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { supplier });

        var service = new OrderRoutingService(db, repo.Object);
        var request = new OrderRequest
        {
            OrderId = "ORD-DUP",
            CustomerZip = "10001",
            MailOrder = false,
            Items = new List<OrderItem>
            {
                new() { ProductCode = "A001", Quantity = 2 },
                new() { ProductCode = "A001", Quantity = 1 },
            }
        };

        var result = await service.RouteAsync(request);

        Assert.True(result.Feasible);
        Assert.Single(result.Routing);
        Assert.Equal(2, result.Routing[0].Items.Count); // both line items preserved under one supplier
    }

    // Scenario 10: Two unknown product codes → only first is reported
    [Fact]
    public async Task Route_MultipleUnknownProductCodes_ReportsFirstUnknownOnly()
    {
        var db = BuildDb(); // no products seeded

        var repo = new Mock<ISupplierRepository>();
        var service = new OrderRoutingService(db, repo.Object);
        var request = new OrderRequest
        {
            OrderId = "ORD-MULTI-UNK",
            CustomerZip = "10001",
            MailOrder = false,
            Items = new List<OrderItem>
            {
                new() { ProductCode = "UNKNOWN-1", Quantity = 1 },
                new() { ProductCode = "UNKNOWN-2", Quantity = 1 },
            }
        };

        var result = await service.RouteAsync(request);

        Assert.False(result.Feasible);
        Assert.Contains(result.Errors!, e => e.Contains("UNKNOWN-1"));
        Assert.DoesNotContain(result.Errors!, e => e.Contains("UNKNOWN-2"));
    }

    // Scenario 11: Nationwide supplier always returns fulfillment_mode "local"
    [Fact]
    public async Task Route_NationwideSupplier_FulfillmentModeIsLocal()
    {
        var db = BuildDb(new Product { Id = 1, ProductCode = "A001", ProductName = "Widget A", Category = "widgets" });

        var nationwide = new Supplier
        {
            Id = 1, SupplierId = "SUP-NAT", SupplierName = "Nationwide Co", SatisfactionScore = 8,
            CanMailOrder = false, ServesNationwide = true,
            ServiceZips = new List<SupplierServiceZip>(), // no explicit ZIPs — relies on ServesNationwide
            ProductCategories = new List<SupplierProductCategory> { new() { Category = "widgets" } },
        };

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync("99999", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier> { nationwide });

        var service = new OrderRoutingService(db, repo.Object);
        var result = await service.RouteAsync(SimpleRequest("99999", false, "A001"));

        Assert.True(result.Feasible);
        Assert.Equal("local", result.Routing[0].Items[0].FulfillmentMode);
    }

    // Scenario 12: Two items in same category fall back to one supplier in multi-supplier mode
    [Fact]
    public async Task Route_TwoItemsSameCategory_MultiSupplierFallback_SingleRoutingEntry()
    {
        var db = BuildDb(
            new Product { Id = 1, ProductCode = "WC-001", ProductName = "Wheelchair A", Category = "wheelchair" },
            new Product { Id = 2, ProductCode = "WC-002", ProductName = "Wheelchair B", Category = "wheelchair" });

        var wheelchairSupplier = LocalSupplier("SUP-001", "Wheelchair Co", "10001", "wheelchair", 8m);

        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetEligibleSuppliersForAllCategoriesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Supplier>()); // no single supplier consolidation
        repo.Setup(r => r.GetEligibleSuppliersByCategoryAsync("10001", false, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, List<Supplier>>
            {
                ["wheelchair"] = new List<Supplier> { wheelchairSupplier }
            });

        var service = new OrderRoutingService(db, repo.Object);
        var request = new OrderRequest
        {
            OrderId = "ORD-SAME-CAT",
            CustomerZip = "10001",
            MailOrder = false,
            Items = new List<OrderItem>
            {
                new() { ProductCode = "WC-001", Quantity = 1 },
                new() { ProductCode = "WC-002", Quantity = 1 },
            }
        };

        var result = await service.RouteAsync(request);

        Assert.True(result.Feasible);
        Assert.Single(result.Routing); // both items grouped under the single wheelchair supplier
        Assert.Equal(2, result.Routing[0].Items.Count);
        Assert.Equal("SUP-001", result.Routing[0].SupplierId);
    }
}
