namespace OrderRouter.Api.Models;

public class SupplierProductCategory
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Category { get; set; } = string.Empty;
}
