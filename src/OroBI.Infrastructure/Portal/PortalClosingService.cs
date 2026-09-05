using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Application.Closings;
using OroBI.Application.Portal;
using OroBI.Domain.Closings;
using OroBI.Infrastructure.Imports;
using OroBI.Infrastructure.Persistence;

namespace OroBI.Infrastructure.Portal;

public sealed class PortalClosingService(OroBiDbContext db, ISellerClosingQueryService calculator) : IPortalClosingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PortalClosing?> GetAsync(Guid sellerId, string importedName, int year, int month, CancellationToken cancellationToken)
    {
        _ = new DateOnly(year, month, 1);
        var snapshot = await FindAsync(sellerId, year, month, cancellationToken);
        var summary = snapshot?.Status == ClosingApprovalStatus.Aprovado
            ? JsonSerializer.Deserialize<SellerClosingSummary>(snapshot.SnapshotJson!, JsonOptions)
            : await calculator.GetAsync(importedName, year, month, cancellationToken);
        return summary is null ? null : PortalClosing.FromSummary(summary, year, month,
            snapshot?.Status.ToString() ?? "EmApuracao", snapshot?.ApprovedAtUtc);
    }

    public async Task<IReadOnlyList<PortalClosingMonth>> GetHistoryAsync(Guid sellerId, string importedName, CancellationToken cancellationToken)
    {
        var names = SellerAliasCatalog.GetMatchingNames(importedName);
        var duplicates = await ImportedBatchSelection.GetDuplicateIdsAsync(db, cancellationToken);
        var periods = await db.CommercialMovements.AsNoTracking()
            .Where(item => names.Contains(item.Seller.Trim().ToUpper()) && !duplicates.Contains(item.ImportBatchId))
            .Select(item => new { item.MovementDate.Year, item.MovementDate.Month }).Distinct().ToListAsync(cancellationToken);
        var snapshots = await db.ClosingSnapshots.AsNoTracking().Where(item => item.SellerId == sellerId).ToListAsync(cancellationToken);
        var statuses = snapshots.ToDictionary(item => (item.Year, item.Month), item => item.Status.ToString());
        return periods.Select(item => (item.Year, item.Month)).Concat(statuses.Keys).Distinct()
            .OrderByDescending(item => item.Year).ThenByDescending(item => item.Month).Take(120)
            .Select(item => new PortalClosingMonth($"{item.Year:D4}-{item.Month:D2}", statuses.GetValueOrDefault(item, "EmApuracao"))).ToArray();
    }

    public async Task<PortalClosing> ReviewAsync(Guid sellerId, string importedName, int year, int month, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        _ = new DateOnly(year, month, 1);
        var existing = await FindAsync(sellerId, year, month, cancellationToken);
        if (existing is not null) throw new InvalidOperationException("O fechamento já está em conferência ou aprovado.");
        var summary = await RequireSummaryAsync(importedName, year, month, cancellationToken);
        var snapshot = ClosingSnapshot.Review(sellerId, year, month, actor);
        db.ClosingSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return PortalClosing.FromSummary(summary, year, month, snapshot.Status.ToString(), null);
    }

    public async Task<PortalClosing> ApproveAsync(Guid sellerId, string importedName, int year, int month, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var snapshot = await FindAsync(sellerId, year, month, cancellationToken)
            ?? throw new InvalidOperationException("Coloque o fechamento em conferência antes de aprovar.");
        if (snapshot.Status == ClosingApprovalStatus.Aprovado) throw new InvalidOperationException("O fechamento aprovado é imutável.");
        var summary = await RequireSummaryAsync(importedName, year, month, cancellationToken);
        snapshot.Approve(JsonSerializer.Serialize(summary, JsonOptions), actor);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return PortalClosing.FromSummary(summary, year, month, snapshot.Status.ToString(), snapshot.ApprovedAtUtc);
    }

    private Task<ClosingSnapshot?> FindAsync(Guid sellerId, int year, int month, CancellationToken cancellationToken) =>
        db.ClosingSnapshots.SingleOrDefaultAsync(item => item.SellerId == sellerId && item.Year == year && item.Month == month, cancellationToken);

    private async Task<SellerClosingSummary> RequireSummaryAsync(string importedName, int year, int month, CancellationToken cancellationToken)
    {
        var result = await calculator.GetAsync(importedName, year, month, cancellationToken);
        if (result is null || result.Monthly.MovementCount == 0)
            throw new InvalidOperationException("Fechamento indisponível: confira os movimentos e configurações do período.");
        return result;
    }
}
