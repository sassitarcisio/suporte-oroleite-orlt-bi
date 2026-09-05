using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace OroBI.Api.Auth;

public sealed class RegistrationRateLimiter : IDisposable
{
    private readonly FixedWindowRateLimiter overall = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
    });
    private readonly PartitionedRateLimiter<string> accounts = PartitionedRateLimiter.Create<string, string>(
        account => RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3, Window = TimeSpan.FromMinutes(15), QueueLimit = 0
        }));

    public RateLimitLease TryAcquire(string email)
    {
        // Bound expensive work and new partitions before accounting for an individual email.
        // A global instance limit avoids penalizing legitimate users sharing the ACA ingress address.
        var total = overall.AttemptAcquire();
        if (!total.IsAcquired) return total;
        total.Dispose();
        var normalized = email.Trim().ToUpperInvariant();
        return accounts.AttemptAcquire(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))));
    }

    public void Dispose() { overall.Dispose(); accounts.Dispose(); }
}
