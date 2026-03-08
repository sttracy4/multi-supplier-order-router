using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OrderRouter.Api.Models;

namespace OrderRouter.Api.Data.Seeding;

public static class ProductCsvParser
{
    public static IReadOnlyList<Product> Parse(string filePath, ILogger logger)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = args =>
                logger.LogWarning("Skipping bad CSV data at row {Row} in products.csv: {Field}",
                    args.Context.Parser?.Row, args.Field),
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        var products = new List<Product>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Read header
        try { csv.Read(); csv.ReadHeader(); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read products.csv header — aborting product seed.");
            return products;
        }

        while (true)
        {
            // Advance to next row
            bool hasRow;
            try { hasRow = csv.Read(); }
            catch (CsvHelperException ex)
            {
                logger.LogWarning(ex, "Skipping unreadable row in products.csv at row {Row}.",
                    csv.Context.Parser?.Row);
                continue;
            }

            if (!hasRow) break;

            // Map row to record
            ProductCsvRecord record;
            try { record = csv.GetRecord<ProductCsvRecord>()!; }
            catch (CsvHelperException ex)
            {
                logger.LogWarning(ex, "Skipping unmappable product row at row {Row}.",
                    csv.Context.Parser?.Row);
                continue;
            }

            // Normalize fields (guard against null from missing columns)
            var productCode = (record.ProductCode ?? string.Empty).Trim();
            var productName = (record.ProductName ?? string.Empty).Trim();
            var category = (record.Category ?? string.Empty).Trim().ToLowerInvariant();

            // Required field guards
            if (string.IsNullOrEmpty(productCode))
            {
                logger.LogWarning("Skipping product row at row {Row}: missing product_code.",
                    csv.Context.Parser?.Row);
                continue;
            }

            if (!seenCodes.Add(productCode))
            {
                logger.LogDebug("Skipping duplicate product_code '{ProductCode}' at row {Row}.",
                    productCode, csv.Context.Parser?.Row);
                continue;
            }

            if (string.IsNullOrEmpty(category))
            {
                logger.LogWarning("Skipping product '{ProductCode}' at row {Row}: missing category.",
                    productCode, csv.Context.Parser?.Row);
                continue;
            }

            // Fall back to product_code as name if name is blank
            if (string.IsNullOrEmpty(productName))
            {
                logger.LogWarning("Product '{ProductCode}' at row {Row} has no name — using product_code as name.",
                    productCode, csv.Context.Parser?.Row);
                productName = productCode;
            }

            products.Add(new Product
            {
                ProductCode = productCode,
                ProductName = productName,
                Category = category,
            });
        }

        return products;
    }
}

public class ProductCsvRecord
{
    [CsvHelper.Configuration.Attributes.Name("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [CsvHelper.Configuration.Attributes.Name("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [CsvHelper.Configuration.Attributes.Name("category")]
    public string Category { get; set; } = string.Empty;
}
