namespace OroBI.Application.Synchronization;

public interface IFirebirdCommercialReader
{
    Task<SynchronizationPage> ReadPageAsync(
        string? watermark,
        int pageSize,
        CancellationToken cancellationToken);
}
