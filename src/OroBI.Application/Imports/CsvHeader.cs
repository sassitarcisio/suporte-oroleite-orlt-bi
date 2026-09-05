namespace OroBI.Application.Imports;

public static class CsvHeader
{
    public static string Normalize(string value)
    {
        var normalized = value.Trim().TrimStart('\uFEFF');
        if (normalized.StartsWith("\u00EF\u00BB\u00BF", StringComparison.Ordinal)) normalized = normalized[3..];
        return normalized.Trim().ToUpperInvariant();
    }
}
