using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace OroBI.Api.Auth;

public sealed class LoginRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        account => RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));

    public RateLimitLease TryAcquire(string? email)
    {
        // ACA clients can share an ingress address; account limits do not trust client-supplied forwarding headers.
        var normalizedEmail = email?.Trim().ToUpperInvariant() ?? string.Empty;
        var accountKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
        return _limiter.AttemptAcquire(accountKey);
    }

    public void Dispose() => _limiter.Dispose();
}
