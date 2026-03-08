using System.ComponentModel.DataAnnotations;

namespace OrderRouter.Api.DTOs;

public class OrderRequest
{
    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "Order must include an order_id.")]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Order must include a valid customer_zip.")]
    public string CustomerZip { get; set; } = string.Empty;

    public bool MailOrder { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Order must include at least one line item.")]
    public List<OrderItem> Items { get; set; } = new();

    public string? Priority { get; set; }
    public string? Notes { get; set; }
}

public class OrderItem
{
    [Required]
    public string ProductCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Item quantity must be at least 1.")]
    public int Quantity { get; set; }
}
