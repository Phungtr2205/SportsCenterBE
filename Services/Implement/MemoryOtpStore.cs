using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SportsCenterAPI.Services.Implement;

public sealed class MemoryOtpStore(IMemoryCache cache)
{
    // ponytail: khóa chung trong một instance; cần kho dùng chung nếu chạy nhiều instance.
    private readonly object _gate = new();

    private sealed class OtpEntry
    {
        public required string Hash { get; init; }
        public required string Payload { get; init; }
        public int FailedAttempts { get; set; }
    }

    public Task SaveAsync<T>(string key, string otpHash, T value)
    {
        var payload = JsonSerializer.Serialize(value);

        lock (_gate)
        {
            cache.Set(key, new OtpEntry
            {
                Hash = otpHash,
                Payload = payload
            }, TimeSpan.FromMinutes(5));
        }

        return Task.CompletedTask;
    }

    public Task<T?> ConsumeAsync<T>(string key, string otpHash) where T : class
    {
        string payload;

        lock (_gate)
        {
            if (!cache.TryGetValue<OtpEntry>(key, out var entry) || entry is null)
                return Task.FromResult<T?>(null);

            if (!string.Equals(entry.Hash, otpHash, StringComparison.Ordinal))
            {
                if (++entry.FailedAttempts >= 5)
                    cache.Remove(key);

                return Task.FromResult<T?>(null);
            }

            payload = entry.Payload;
            cache.Remove(key);
        }

        return Task.FromResult(JsonSerializer.Deserialize<T>(payload));
    }
}
