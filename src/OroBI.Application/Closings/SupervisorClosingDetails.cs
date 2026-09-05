namespace OroBI.Application.Closings;

public sealed record ClosingOperation(string Key, string Label, decimal Revenue, decimal Trade, decimal TradeReturns)
{
    public decimal TotalTrades => Trade + TradeReturns;
    public decimal TradePercent => Revenue > 0m ? TotalTrades / Revenue * 100m : 0m;
}

public sealed record SupervisorTeamMember(string Seller, bool IncludedInPayroll,
    ClosingOperation Sales, decimal PppAward, decimal GoalAward)
{
    public decimal TotalAward => PppAward + GoalAward;
}

public sealed record SupervisorClosingDetails(decimal OwnCommission, decimal TeamCommission,
    decimal NetworkCommission, IReadOnlyList<ClosingOperation> Operations,
    IReadOnlyList<SupervisorTeamMember> Team, decimal TeamAverageAward, decimal PayrollTeamAverageAward);
