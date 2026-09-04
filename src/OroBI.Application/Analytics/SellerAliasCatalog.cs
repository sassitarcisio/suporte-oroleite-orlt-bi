namespace OroBI.Application.Analytics;

public static class SellerAliasCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ImportedNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MARCELO DA ROSA"] = "MARCELO IVONEI DA ROSA"
    };

    public static string ResolveImportedName(string seller)
    {
        var normalized = seller.Trim().ToUpperInvariant();
        return ImportedNames.TryGetValue(normalized, out var importedName) ? importedName : normalized;
    }
}
