using System.ComponentModel.DataAnnotations;

namespace OrderRouter.Api.DTOs;

public class OrderRequest
{
    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "customer_zip must be exactly 5 digits.")]
    public string CustomerZip { get; set; } = string.Empty;

    public bool MailOrder { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<OrderItem> Items { get; set; } = new();

    public string? Priority { get; set; }
    public string? Notes { get; set; }
}

public class OrderItem
{
    [Required]
    public string ProductCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}
