using CsvHelper.Configuration;

namespace OrderRouter.Api.Data.Seeding;

public class SupplierCsvRecord
{
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string ServiceZips { get; set; } = string.Empty;
    public string ProductCategories { get; set; } = string.Empty;
    public string SatisfactionScore { get; set; } = string.Empty;
    public string CanMailOrder { get; set; } = string.Empty;
}

public sealed class SupplierCsvMap : ClassMap<SupplierCsvRecord>
{
    public SupplierCsvMap()
    {
        Map(m => m.SupplierId).Name("supplier_id");
        Map(m => m.SupplierName).Name("suplier_name");       // matches actual CSV header typo
        Map(m => m.ServiceZips).Name("service_zips");
        Map(m => m.ProductCategories).Name("product_categories");
        Map(m => m.SatisfactionScore).Name("customer_satisfaction_score");
        Map(m => m.CanMailOrder).Name("can_mail_order?");    // matches actual CSV header with ?
    }
}
