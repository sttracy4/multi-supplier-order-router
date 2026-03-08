using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OrderRouter.Api.Data.Seeding;
using Xunit;

namespace OrderRouter.Api.Tests.Unit;

public class SupplierCsvParserTests
{
    private static readonly NullLogger<SupplierCsvParserTests> Logger = NullLogger<SupplierCsvParserTests>.Instance;

    private static string WriteTempCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    [Fact]
    public void Parse_ValidRow_MapsAllFields()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-001,Acme Medical,10001-10003,wheelchair,8.5,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            var s = suppliers[0];
            Assert.Equal("SUP-001", s.SupplierId);
            Assert.Equal("Acme Medical", s.SupplierName);
            Assert.Equal(8.5m, s.SatisfactionScore);
            Assert.False(s.CanMailOrder);
            Assert.False(s.ServesNationwide);
            Assert.Equal(3, s.ServiceZips.Count);
            Assert.Single(s.ProductCategories);
            Assert.Equal("wheelchair", s.ProductCategories.First().Category);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NoRatingsYet_SetsScoreToZero()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-002,No Rating Co,10001,oxygen,no ratings yet,y
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Equal(0m, suppliers[0].SatisfactionScore);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_CanMailOrderY_SetsTrue()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-003,Mail Co,10001,cpap,7,y
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.True(suppliers[0].CanMailOrder);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NationwideRange_SetsServesNationwideFlag()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-004,National Co,00100-99999,wheelchair,9,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.True(suppliers[0].ServesNationwide);
            Assert.Empty(suppliers[0].ServiceZips);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MultipleCategories_NormalizesLowercase()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-005,Multi Cat,10001,"Wheelchair, Oxygen, CPAP",8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            var cats = suppliers[0].ProductCategories.Select(c => c.Category).ToList();
            Assert.Equal(3, cats.Count);
            Assert.Contains("wheelchair", cats);
            Assert.Contains("oxygen", cats);
            Assert.Contains("cpap", cats);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_BostonStyleZipRange_PadsLeadingZeros()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-006,Boston Co,2164-2166,oxygen,8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            var zips = suppliers[0].ServiceZips.Select(z => z.Zip).ToList();
            Assert.Contains("02164", zips);
            Assert.Contains("02165", zips);
            Assert.Contains("02166", zips);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MissingSupplierId_SkipsRow()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            ,No ID Co,10001,wheelchair,8,n
            SUP-007,Valid Co,10001,wheelchair,8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            Assert.Equal("SUP-007", suppliers[0].SupplierId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NoCategories_SkipsRow()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-008,No Cat Co,10001,,8,n
            SUP-009,Valid Co,10001,wheelchair,8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            Assert.Equal("SUP-009", suppliers[0].SupplierId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NoValidZipsAndNotNationwide_SkipsRow()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-010,No Zip Co,INVALID,wheelchair,8,n
            SUP-011,Valid Co,10001,wheelchair,8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            Assert.Equal("SUP-011", suppliers[0].SupplierId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_DuplicateSupplierId_KeepsFirstOccurrence()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-012,First Co,10001,wheelchair,9,n
            SUP-012,Second Co,10002,oxygen,5,y
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            Assert.Equal("First Co", suppliers[0].SupplierName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NonNumericZipTokensSkipped_ValidZipsRetained()
    {
        var csv = """
            supplier_id,suplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?
            SUP-013,Mixed Zip Co,"10001,N/A,10003,abc",wheelchair,8,n
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var suppliers = SupplierCsvParser.Parse(path, Logger);
            Assert.Single(suppliers);
            var zips = suppliers[0].ServiceZips.Select(z => z.Zip).ToList();
            Assert.Equal(2, zips.Count);
            Assert.Contains("10001", zips);
            Assert.Contains("10003", zips);
        }
        finally { File.Delete(path); }
    }
}
