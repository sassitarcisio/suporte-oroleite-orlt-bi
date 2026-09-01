namespace OroBI.Domain.Goals;

public sealed class GoalRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; private set; }
    public string Seller { get; private set; } = string.Empty;
    public int Month { get; private set; }
    public int Year { get; private set; }
    public string GoalType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Target { get; private set; }
    public decimal Achieved { get; private set; }

    public static GoalRecord Create(Guid importBatchId, string seller, int month, int year, string goalType, string description, decimal target, decimal achieved) => new()
    {
        ImportBatchId = importBatchId,
        Seller = seller,
        Month = month,
        Year = year,
        GoalType = goalType,
        Description = description,
        Target = target,
        Achieved = achieved
    };
}
