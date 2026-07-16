using Microsoft.Extensions.Caching.Memory;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Cloud;

public sealed class MemoryCloudOAuthStateStore : ICloudOAuthStateStore
{
    private const string KeyPrefix = "cloud-oauth-state:";
    private readonly IMemoryCache _cache;

    public MemoryCloudOAuthStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task StoreAsync(string state, string userId, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _cache.Set(KeyPrefix + state, userId, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        });

        return Task.CompletedTask;
    }

    public Task<string?> ConsumeAsync(string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return Task.FromResult<string?>(null);
        }

        var key = KeyPrefix + state;
        if (!_cache.TryGetValue(key, out string? userId))
        {
            return Task.FromResult<string?>(null);
        }

        _cache.Remove(key);
        return Task.FromResult(userId);
    }
}
