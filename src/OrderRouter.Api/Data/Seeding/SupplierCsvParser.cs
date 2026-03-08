using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OrderRouter.Api.Models;
using OrderRouter.Api.Utilities;

namespace OrderRouter.Api.Data.Seeding;

public static class SupplierCsvParser
{
    public static IReadOnlyList<Supplier> Parse(string filePath, ILogger logger)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = args =>
                logger.LogWarning("Skipping bad CSV data at row {Row} in suppliers.csv: {Field}",
                    args.Context.Parser?.Row, args.Field),
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<SupplierCsvMap>();

        var suppliers = new List<Supplier>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Read header
        try { csv.Read(); csv.ReadHeader(); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read suppliers.csv header — aborting supplier seed.");
            return suppliers;
        }

        while (true)
        {
            // Advance to next row
            bool hasRow;
            try { hasRow = csv.Read(); }
            catch (CsvHelperException ex)
            {
                logger.LogWarning(ex, "Skipping unreadable row in suppliers.csv at row {Row}.",
                    csv.Context.Parser?.Row);
                continue;
            }

            if (!hasRow) break;

            // Map row to record
            SupplierCsvRecord record;
            try { record = csv.GetRecord<SupplierCsvRecord>()!; }
            catch (CsvHelperException ex)
            {
                logger.LogWarning(ex, "Skipping unmappable supplier row at row {Row}.",
                    csv.Context.Parser?.Row);
                continue;
            }

            // Normalize fields (guard against null from missing columns)
            var supplierId = (record.SupplierId ?? string.Empty).Trim();
            var supplierName = (record.SupplierName ?? string.Empty).Trim();
            var serviceZips = (record.ServiceZips ?? string.Empty).Trim();
            var productCategories = (record.ProductCategories ?? string.Empty).Trim();
            var satisfactionScore = (record.SatisfactionScore ?? string.Empty).Trim();
            var canMailOrder = (record.CanMailOrder ?? string.Empty).Trim();

            // Required field guards
            if (string.IsNullOrEmpty(supplierId))
            {
                logger.LogWarning("Skipping supplier row at row {Row}: missing supplier_id.",
                    csv.Context.Parser?.Row);
                continue;
            }

            if (!seenIds.Add(supplierId))
            {
                logger.LogDebug("Skipping duplicate supplier_id '{SupplierId}' at row {Row}.",
                    supplierId, csv.Context.Parser?.Row);
                continue;
            }

            if (string.IsNullOrEmpty(supplierName))
            {
                logger.LogWarning("Skipping supplier '{SupplierId}' at row {Row}: missing supplier name.",
                    supplierId, csv.Context.Parser?.Row);
                continue;
            }

            // Parse categories — supplier must have at least one to be routable
            var categories = productCategories
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.ToLowerInvariant())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();

            if (categories.Count == 0)
            {
                logger.LogWarning("Skipping supplier '{SupplierId}' at row {Row}: no product categories.",
                    supplierId, csv.Context.Parser?.Row);
                continue;
            }

            // Parse service area
            var (zips, isNationwide) = ZipRangeParser.Expand(serviceZips, logger);

            if (!isNationwide && zips.Count == 0)
            {
                logger.LogWarning("Skipping supplier '{SupplierId}' at row {Row}: no valid service ZIPs and not nationwide.",
                    supplierId, csv.Context.Parser?.Row);
                continue;
            }

            var supplier = new Supplier
            {
                SupplierId = supplierId,
                SupplierName = supplierName,
                CanMailOrder = canMailOrder.Equals("y", StringComparison.OrdinalIgnoreCase),
                SatisfactionScore = ParseScore(satisfactionScore),
                ServesNationwide = isNationwide,
            };

            foreach (var zip in zips)
                supplier.ServiceZips.Add(new SupplierServiceZip { Zip = zip });

            foreach (var cat in categories)
                supplier.ProductCategories.Add(new SupplierProductCategory { Category = cat });

            suppliers.Add(supplier);
        }

        return suppliers;
    }

    private static decimal ParseScore(string raw)
    {
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var score))
            return score;
        return 0m; // "no ratings yet" or any non-numeric value
    }
}
