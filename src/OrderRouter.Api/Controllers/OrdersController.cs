using Microsoft.AspNetCore.Mvc;
using OrderRouter.Api.DTOs;
using OrderRouter.Api.Services;

namespace OrderRouter.Api.Controllers;

[ApiController]
[Route("api")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRoutingService _routingService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderRoutingService routingService, ILogger<OrdersController> logger)
    {
        _routingService = routingService;
        _logger = logger;
    }

    /// <summary>Route a multi-item order to the optimal supplier(s).</summary>
    [HttpPost("route")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Route([FromBody] OrderRequest? request)
    {
        // Validation errors → feasible: false with all error messages
        if (request is null)
            return Ok(Infeasible("Request body is required."));

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();
            return Ok(new OrderResponse { Feasible = false, Errors = errors });
        }

        try
        {
            var result = await _routingService.RouteAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception routing order {OrderId}", request.OrderId);
            return Ok(Infeasible("An internal error occurred while processing the order."));
        }
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy" });

    private static OrderResponse Infeasible(string error) =>
        new() { Feasible = false, Errors = new List<string> { error } };
}
