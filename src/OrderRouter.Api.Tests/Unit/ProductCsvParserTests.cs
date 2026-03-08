using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OrderRouter.Api.Data.Seeding;
using Xunit;

namespace OrderRouter.Api.Tests.Unit;

public class ProductCsvParserTests
{
    private static readonly NullLogger<ProductCsvParserTests> Logger = NullLogger<ProductCsvParserTests>.Instance;

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
            product_code,product_name,category
            WC-STD-001,Standard Wheelchair,wheelchair
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Single(products);
            Assert.Equal("WC-STD-001", products[0].ProductCode);
            Assert.Equal("Standard Wheelchair", products[0].ProductName);
            Assert.Equal("wheelchair", products[0].Category);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_Category_NormalizesToLowercase()
    {
        var csv = """
            product_code,product_name,category
            OX-PORT-001,Portable Oxygen,OXYGEN
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Equal("oxygen", products[0].Category);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MissingProductCode_SkipsRow()
    {
        var csv = """
            product_code,product_name,category
            ,No Code Product,wheelchair
            WC-STD-002,Valid Product,wheelchair
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Single(products);
            Assert.Equal("WC-STD-002", products[0].ProductCode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MissingCategory_SkipsRow()
    {
        var csv = """
            product_code,product_name,category
            WC-BAD-001,No Category Product,
            WC-STD-003,Valid Product,wheelchair
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Single(products);
            Assert.Equal("WC-STD-003", products[0].ProductCode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_DuplicateProductCode_KeepsFirstOccurrence()
    {
        var csv = """
            product_code,product_name,category
            WC-DUP-001,First Name,wheelchair
            WC-DUP-001,Second Name,oxygen
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Single(products);
            Assert.Equal("First Name", products[0].ProductName);
            Assert.Equal("wheelchair", products[0].Category);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MissingProductName_FallsBackToProductCode()
    {
        var csv = """
            product_code,product_name,category
            WC-NONAME-001,,wheelchair
            """;
        var path = WriteTempCsv(csv);
        try
        {
            var products = ProductCsvParser.Parse(path, Logger);
            Assert.Single(products);
            Assert.Equal("WC-NONAME-001", products[0].ProductName);
        }
        finally { File.Delete(path); }
    }
}
