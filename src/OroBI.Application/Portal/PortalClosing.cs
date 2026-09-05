using OroBI.Application.Closings;

namespace OroBI.Application.Portal;

public sealed record PortalClosing(int Year, int Month, string Status, bool IsEstimated, DateTimeOffset? ApprovedAtUtc,
    decimal? Revenue, decimal? CommissionableRevenue, decimal? Commission, decimal? CommissionPercent,
    decimal? PppPercent, decimal? PppAward, decimal? RevenueAward, decimal? PositivityAward, decimal? TradeAward,
    decimal? TotalAwards, decimal? TradeValue, decimal? TradePercent, decimal? CommissionAndAwards)
{
    public static PortalClosing FromSummary(SellerClosingSummary source, int year, int month, string status, DateTimeOffset? approvedAt) =>
        new(year, month, status, status != "Aprovado", approvedAt, source.Monthly.Scope == "seller" ? source.Monthly.Revenue : null,
            source.Monthly.Scope == "seller" ? source.Monthly.CommissionableRevenue : null, source.Compensation.Commission,
            source.Monthly.Scope == "seller" ? source.CommissionPercent : null,
            source.Ppp.MeanPercent, source.Ppp.Award, source.RevenueAward, source.PositivityAward, source.TradeAward,
            source.TotalAwards, source.Monthly.Scope == "seller" ? source.Monthly.TradeValue : null,
            source.Monthly.Scope == "seller" ? source.Monthly.TradePercent : null,
            source.Compensation.Commission + source.TotalAwards);
}

public sealed record PortalClosingMonth(string Month, string Status);

public interface IPortalClosingService
{
    Task<PortalClosing?> GetAsync(Guid sellerId, string importedName, int year, int month, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortalClosingMonth>> GetHistoryAsync(Guid sellerId, string importedName, CancellationToken cancellationToken);
    Task<PortalClosing> ReviewAsync(Guid sellerId, string importedName, int year, int month, string actor, CancellationToken cancellationToken);
    Task<PortalClosing> ApproveAsync(Guid sellerId, string importedName, int year, int month, string actor, CancellationToken cancellationToken);
}
