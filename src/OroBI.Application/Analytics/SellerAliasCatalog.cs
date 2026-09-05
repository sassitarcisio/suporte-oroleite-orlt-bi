namespace OroBI.Application.Analytics;

public static class SellerAliasCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ImportedNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MARCIO FERNANDES"] = "VENDEDOR: MARCIO FERNANDES",
        ["MARCIO LUIZ DA ROSA"] = "VENDEDOR: MARCIO LUIZ DA ROSA",
        ["ANDERSON GONCALVES SOUZA"] = "VENDEDOR: ANDERSON GONCALVES SOUZA",
        ["DEIVID MANNES"] = "SUPERVISOR: DEIVID MANNES",
        ["RODRIGO KEHL"] = "VENDEDOR: RODRIGO",
        ["RODRIGO"] = "VENDEDOR: RODRIGO",
        ["MARCELO DA ROSA"] = "VENDEDOR: MARCELO IVONEI DA ROSA",
        ["MARCELO IVONEI DA ROSA"] = "VENDEDOR: MARCELO IVONEI DA ROSA",
        ["PAULO RICARDO LOPES"] = "VENDEDOR: PAULO RICARDO LOPES",
        ["RAMON DO NASCIMENTO"] = "VENDEDOR: RAMON DO NASCIMENTO",
        ["TIAGO MARTINS"] = "VENDEDOR: TIAGO MARTINS"
    };

    public static string ResolveImportedName(string seller)
    {
        var normalized = WithoutRolePrefix(seller.Trim().ToUpperInvariant());
        return ImportedNames.TryGetValue(normalized, out var importedName) ? importedName : normalized;
    }

    public static string[] GetMatchingNames(string seller)
    {
        var importedName = ResolveImportedName(seller);
        var names = ImportedNames.Where(pair => pair.Value == importedName).Select(pair => pair.Key)
            .Append(WithoutRolePrefix(importedName));
        return names.SelectMany(name => new[] { name, $"VENDEDOR: {name}", $"SUPERVISOR: {name}" })
            .Append(importedName).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string WithoutRolePrefix(string name)
    {
        // Prefixes describe the imported row, not a separate person. Resolve aliases only after removing them.
        while (true)
        {
            if (name.StartsWith("VENDEDOR:", StringComparison.Ordinal)) name = name[9..].Trim();
            else if (name.StartsWith("SUPERVISOR:", StringComparison.Ordinal)) name = name[11..].Trim();
            else return name;
        }
    }
}
