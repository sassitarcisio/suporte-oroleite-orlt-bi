namespace OroBI.Application.Identity;

public interface ILocalAuthenticationService
{
    Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed record LocalLoginResult(string AccessToken, DateTime ExpiresAtUtc, IReadOnlyCollection<string> Roles);
