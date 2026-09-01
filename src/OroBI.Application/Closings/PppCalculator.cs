namespace OroBI.Application.Closings;

public static class PppCalculator
{
    public static PppSummary Calculate(decimal maximumAward, IEnumerable<(decimal CustomerCount, decimal ItemsPerSegment, decimal GroupsPlaced)> segments)
    {
        var rates = segments
            .Where(segment => segment.CustomerCount > 0m && segment.ItemsPerSegment > 0m)
            .Select(segment => segment.GroupsPlaced / (segment.CustomerCount * segment.ItemsPerSegment) * 100m)
            .ToArray();
        var meanPercent = rates.Length == 0 ? 0m : rates.Average();

        return new PppSummary(meanPercent, maximumAward * meanPercent / 100m);
    }
}
