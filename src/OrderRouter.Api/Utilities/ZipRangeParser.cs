namespace OrderRouter.Api.Utilities;

public static class ZipRangeParser
{
    private const int NationwideThreshold = 99_900;

    /// <summary>
    /// Returns (zips, isNationwide). If the total count would exceed NationwideThreshold,
    /// returns (empty, true) so the caller can set the ServesNationwide flag instead.
    /// Non-numeric or malformed tokens are skipped (logged if logger provided).
    /// </summary>
    public static (IReadOnlyList<string> Zips, bool IsNationwide) Expand(
        string rawServiceZips, ILogger? logger = null)
    {
        var tokens = rawServiceZips.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var token in tokens)
        {
            var dashIndex = token.IndexOf('-');
            if (dashIndex > 0
                && int.TryParse(token[..dashIndex], out int start)
                && int.TryParse(token[(dashIndex + 1)..], out int end)
                && end >= start)
            {
                int count = end - start + 1;
                if (count >= NationwideThreshold)
                    return (Array.Empty<string>(), true);

                int width = Math.Max(token[..dashIndex].Length, 5);
                for (int z = start; z <= end; z++)
                    result.Add(z.ToString().PadLeft(width, '0'));
            }
            else
            {
                var normalized = NormalizeZip(token);
                if (IsValidZip(normalized))
                {
                    result.Add(normalized);
                }
                else
                {
                    logger?.LogWarning(
                        "Skipping invalid ZIP token '{Token}' — expected 5 digits or numeric range.", token);
                }
            }
        }

        return (result, false);
    }

    public static string NormalizeZip(string zip) =>
        zip.Trim().PadLeft(5, '0');

    private static bool IsValidZip(string zip) =>
        zip.Length == 5 && zip.All(char.IsDigit);
}
