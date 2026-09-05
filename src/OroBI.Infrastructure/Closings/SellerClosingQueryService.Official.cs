using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OroBI.Application.Analytics;
using OroBI.Application.Closings;
using OroBI.Domain.Closings;

namespace OroBI.Infrastructure.Closings;

public sealed partial class SellerClosingQueryService
{
    private async Task<SellerClosingSummary?> GetOfficialOrCurrentAsync(string seller, int year, int month, CancellationToken cancellationToken)
    {
        var names = SellerAliasCatalog.GetMatchingNames(CanonicalClosingSeller(seller));
        var snapshot = await (from closing in dbContext.ClosingSnapshots.AsNoTracking()
                              join registeredSeller in dbContext.Sellers on closing.SellerId equals registeredSeller.Id
                              where names.Contains(registeredSeller.ImportedName) && closing.Year == year && closing.Month == month
                                  && closing.Status == ClosingApprovalStatus.Aprovado
                              select closing.SnapshotJson).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null) return await GetSellerAsync(seller, year, month, false, cancellationToken);
        var approved = JsonSerializer.Deserialize<SellerClosingSummary>(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return approved is null ? null : approved with { IsApproved = true };
    }
}
