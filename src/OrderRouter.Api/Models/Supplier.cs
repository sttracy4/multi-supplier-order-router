namespace OrderRouter.Api.Models;

public class Supplier
{
    public int Id { get; set; }
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal SatisfactionScore { get; set; }
    public bool CanMailOrder { get; set; }
    public bool ServesNationwide { get; set; }

    public ICollection<SupplierServiceZip> ServiceZips { get; set; } = new List<SupplierServiceZip>();
    public ICollection<SupplierProductCategory> ProductCategories { get; set; } = new List<SupplierProductCategory>();
}
