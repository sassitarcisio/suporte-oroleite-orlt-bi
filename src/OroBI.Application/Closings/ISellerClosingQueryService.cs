namespace OroBI.Application.Closings;

public interface ISellerClosingQueryService
{
    Task<SellerClosingSummary?> GetAsync(string seller, int year, int month, CancellationToken cancellationToken);
}
