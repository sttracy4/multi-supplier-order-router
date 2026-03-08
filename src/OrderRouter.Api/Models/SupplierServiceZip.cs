namespace OrderRouter.Api.Models;

public class SupplierServiceZip
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Zip { get; set; } = string.Empty;
}
