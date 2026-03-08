using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrderRouter.Api.Data;
using OrderRouter.Api.DTOs;
using OrderRouter.Api.Services;
using Xunit;

namespace OrderRouter.Api.Tests.Integration;

/// <summary>
/// Verifies that an unhandled exception thrown by the routing service
/// is caught by the controller and returned as HTTP 200 + feasible:false
/// rather than leaking as a 500.
/// </summary>
public class RoutingExceptionTests : IClassFixture<RoutingExceptionTests.ThrowingFactory>
{
    private readonly HttpClient _client;

    public RoutingExceptionTests(ThrowingFactory factory)
    {
        _client = factory.CreateClient();
    }

    public class ThrowingFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbName = "ThrowingTestDb_" + Guid.NewGuid().ToString();

            builder.ConfigureServices(services =>
            {
                // Use in-memory DB so startup migration is skipped
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

                // Replace routing service with one that always throws
                services.RemoveAll<IOrderRoutingService>();
                services.AddScoped<IOrderRoutingService>(_ =>
                {
                    var mock = new Mock<IOrderRoutingService>();
                    mock.Setup(s => s.RouteAsync(It.IsAny<OrderRequest>()))
                        .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));
                    return mock.Object;
                });
            });

            // Skip CSV seeding
            builder.UseSetting("ResourcesPath", "nonexistent");
        }
    }

    // DB exception during routing → controller catches it → HTTP 200, feasible:false
    [Fact]
    public async Task Route_ServiceThrowsException_Returns200WithInfeasible()
    {
        var json = """{"order_id":"ORD-EX","customer_zip":"10001","items":[{"product_code":"ANY-001","quantity":1}]}""";
        var response = await _client.PostAsync("/api/route",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions());
        Assert.False(result!.Feasible);
        Assert.NotEmpty(result.Errors!);
    }

    // Non-route endpoints still return 500 on unhandled exception (not tested here but documented)
    // The global exception handler only converts /api/route exceptions to 200 + infeasible.

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
