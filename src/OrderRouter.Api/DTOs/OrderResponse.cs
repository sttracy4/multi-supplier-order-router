namespace OrderRouter.Api.DTOs;

public class OrderResponse
{
    public bool Feasible { get; set; }
    public string? InfeasibilityReason { get; set; }
    public List<RoutedSupplier> Routing { get; set; } = new();
}

public class RoutedSupplier
{
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public List<RoutedItem> Items { get; set; } = new();
}

public class RoutedItem
{
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FulfillmentMode { get; set; } = string.Empty;
}
