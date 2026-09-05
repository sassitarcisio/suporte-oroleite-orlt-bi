namespace OroBI.Application.Analytics;

public sealed record MarginSummary(decimal Revenue, decimal Cost, decimal GrossProfit, decimal MarginPercent)
{
    public int CustomerCount { get; init; }
    public int ProductCount { get; init; }
    public int MovementCount { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<MarginRow>> Groups { get; init; } =
        new Dictionary<string, IReadOnlyList<MarginRow>>
        {
            ["customer"] = [], ["product"] = [], ["brand"] = []
        };
}

public sealed record MarginRow(string Label, decimal Revenue, decimal Cost, decimal GrossProfit, decimal? MarginPercent, decimal Quantity);
