using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderRouter.Api.Data;
using OrderRouter.Api.DTOs;
using OrderRouter.Api.Models;
using Xunit;

namespace OrderRouter.Api.Tests.Integration;

public class OrdersControllerTests : IClassFixture<OrdersControllerTests.TestWebAppFactory>
{
    private readonly HttpClient _client;

    public OrdersControllerTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Fix: capture the DB name outside the lambda so all DbContext instances share the same in-memory database
            var dbName = "IntegrationTestDb_" + Guid.NewGuid().ToString();

            builder.ConfigureServices(services =>
            {
                // Replace real DbContext with in-memory
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(opts =>
                    opts.UseInMemoryDatabase(dbName));

                // Seed test data after container is built
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedTestData(db);
            });

            // Point resources path to a non-existent dir — seeding is done above in ConfigureServices
            builder.UseSetting("ResourcesPath", "nonexistent");
        }

        private static void SeedTestData(AppDbContext db)
        {
            var wheelchair = new Product { Id = 1, ProductCode = "WC-STD-001", ProductName = "Standard Wheelchair", Category = "wheelchair" };
            var oxygen = new Product { Id = 2, ProductCode = "OX-PORT-024", ProductName = "Portable Oxygen", Category = "oxygen" };
            var cpap = new Product { Id = 3, ProductCode = "CP-STD-031", ProductName = "CPAP Machine", Category = "cpap" };
            db.Products.AddRange(wheelchair, oxygen, cpap);

            // Supplier serving NYC (10001–10099) with wheelchair + oxygen
            var nyc = new Supplier
            {
                Id = 1, SupplierId = "SUP-001", SupplierName = "NYC Medical Supply",
                SatisfactionScore = 8.5m, CanMailOrder = false, ServesNationwide = false,
                ServiceZips = Enumerable.Range(10001, 99).Select(z => new SupplierServiceZip { Zip = z.ToString() }).ToList(),
                ProductCategories = new List<SupplierProductCategory>
                {
                    new() { Category = "wheelchair" }, new() { Category = "oxygen" }
                }
            };

            // Supplier serving NYC with cpap only
            var cpapSupplier = new Supplier
            {
                Id = 2, SupplierId = "SUP-002", SupplierName = "Respiratory NYC",
                SatisfactionScore = 7m, CanMailOrder = false, ServesNationwide = false,
                ServiceZips = new List<SupplierServiceZip> { new() { Zip = "10015" } },
                ProductCategories = new List<SupplierProductCategory> { new() { Category = "cpap" } }
            };

            // National mail-order supplier with all categories
            var national = new Supplier
            {
                Id = 3, SupplierId = "SUP-003", SupplierName = "National DME",
                SatisfactionScore = 6m, CanMailOrder = true, ServesNationwide = false,
                ServiceZips = new List<SupplierServiceZip>(),
                ProductCategories = new List<SupplierProductCategory>
                {
                    new() { Category = "wheelchair" }, new() { Category = "oxygen" }, new() { Category = "cpap" }
                }
            };

            db.Suppliers.AddRange(nyc, cpapSupplier, national);
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Route_ValidOrder_Returns200WithRouting()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-001",
            CustomerZip = "10015",
            MailOrder = false,
            Items = new List<OrderItem>
            {
                new() { ProductCode = "WC-STD-001", Quantity = 1 },
                new() { ProductCode = "OX-PORT-024", Quantity = 1 }
            }
        };

        var response = await PostOrderAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());
        Assert.NotNull(result);
        Assert.True(result!.Feasible);
        Assert.NotEmpty(result.Routing);
    }

    [Fact]
    public async Task Route_LocalOrderConsolidates_SingleSupplier()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-CONSOL",
            CustomerZip = "10015",
            MailOrder = false,
            Items = new List<OrderItem>
            {
                new() { ProductCode = "WC-STD-001", Quantity = 1 },
                new() { ProductCode = "OX-PORT-024", Quantity = 1 }
            }
        };

        var response = await PostOrderAsync(request);
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());

        // SUP-001 serves wheelchair+oxygen at 10015 — should consolidate
        Assert.True(result!.Feasible);
        Assert.Single(result.Routing);
        Assert.Equal("SUP-001", result.Routing[0].SupplierId);
    }

    [Fact]
    public async Task Route_MailOrderTrue_UsesNationalSupplierWhenNeeded()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-MAIL",
            CustomerZip = "99999",  // no local suppliers serve this ZIP
            MailOrder = true,
            Items = new List<OrderItem> { new() { ProductCode = "WC-STD-001", Quantity = 1 } }
        };

        var response = await PostOrderAsync(request);
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());

        Assert.True(result!.Feasible);
        Assert.Equal("mail_order", result.Routing[0].Items[0].FulfillmentMode);
    }

    [Fact]
    public async Task Route_MailOrderFalse_NoLocalSupplier_Infeasible()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-NOOP",
            CustomerZip = "99999",  // no local suppliers
            MailOrder = false,
            Items = new List<OrderItem> { new() { ProductCode = "WC-STD-001", Quantity = 1 } }
        };

        var response = await PostOrderAsync(request);
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(result!.Feasible);
    }

    [Fact]
    public async Task Route_UnknownProductCode_Returns200Infeasible()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-UNK",
            CustomerZip = "10001",
            MailOrder = false,
            Items = new List<OrderItem> { new() { ProductCode = "UNKNOWN-999", Quantity = 1 } }
        };

        var response = await PostOrderAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());
        Assert.False(result!.Feasible);
        Assert.Contains("UNKNOWN-999", result.InfeasibilityReason);
    }

    [Fact]
    public async Task Route_MissingCustomerZip_Returns200WithInfeasible()
    {
        var json = """{"order_id":"ORD-BAD","mail_order":false,"items":[{"product_code":"WC-STD-001","quantity":1}]}""";
        var response = await _client.PostAsync("/api/route",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());
        Assert.False(result!.Feasible);
        Assert.NotNull(result.InfeasibilityReason);
    }

    [Fact]
    public async Task Route_InvalidZipFormat_Returns200WithInfeasible()
    {
        var request = new OrderRequest
        {
            OrderId = "ORD-BADZIP",
            CustomerZip = "ABCDE",
            MailOrder = false,
            Items = new List<OrderItem> { new() { ProductCode = "WC-STD-001", Quantity = 1 } }
        };

        var response = await PostOrderAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());
        Assert.False(result!.Feasible);
        Assert.NotNull(result.InfeasibilityReason);
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static System.Text.Json.JsonSerializerOptions JsonOptions() =>
        new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

    private Task<HttpResponseMessage> PostOrderAsync(OrderRequest request) =>
        _client.PostAsJsonAsync("/api/route", request, JsonOptions());
}
