namespace OroBI.Domain.Goals;

public sealed class GoalValueRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; private set; }
    public string Brand { get; private set; } = string.Empty;
    public decimal RevenuePrize { get; private set; }
    public decimal PositivityPrize { get; private set; }
    public decimal TradePrize { get; private set; }
    public decimal TradePercentageGoal { get; private set; }

    public static GoalValueRecord Create(Guid importBatchId, string brand, decimal revenuePrize, decimal positivityPrize, decimal tradePrize, decimal tradePercentageGoal) => new()
    {
        ImportBatchId = importBatchId,
        Brand = brand,
        RevenuePrize = revenuePrize,
        PositivityPrize = positivityPrize,
        TradePrize = tradePrize,
        TradePercentageGoal = tradePercentageGoal
    };
}
