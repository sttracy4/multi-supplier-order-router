using OrderRouter.Api.DTOs;

namespace OrderRouter.Api.Services;

public interface IOrderRoutingService
{
    Task<OrderResponse> RouteAsync(OrderRequest request);
}
