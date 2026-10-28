using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public sealed class MemoryOtpStoreTests : IDisposable
{
    private readonly TestClock _clock = new();
    private readonly MemoryCache _cache;
    private readonly MemoryOtpStore _store;

    public MemoryOtpStoreTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = _clock });
        _store = new MemoryOtpStore(_cache);
    }

    [Fact]
    public async Task CorrectCode_ReturnsPayloadOnlyOnce()
    {
        await _store.SaveAsync("register:user", "hash", "pending registration");

        Assert.Equal("pending registration", await _store.ConsumeAsync<string>("register:user", "hash"));
        Assert.Null(await _store.ConsumeAsync<string>("register:user", "hash"));
    }

    [Fact]
    public async Task FifthWrongAttempt_InvalidatesCorrectCode()
    {
        await _store.SaveAsync("reset:user", "correct", "user@example.com");
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Null(await _store.ConsumeAsync<string>("reset:user", "wrong"));

        Assert.Null(await _store.ConsumeAsync<string>("reset:user", "correct"));
    }

    [Fact]
    public async Task FourWrongAttempts_StillAllowCorrectCode()
    {
        await _store.SaveAsync("reset:user", "correct", "user@example.com");
        for (var attempt = 0; attempt < 4; attempt++)
            Assert.Null(await _store.ConsumeAsync<string>("reset:user", "wrong"));

        Assert.Equal("user@example.com", await _store.ConsumeAsync<string>("reset:user", "correct"));
    }

    [Fact]
    public async Task WrongAttempt_DoesNotExtendFiveMinuteExpiry()
    {
        await _store.SaveAsync("reset:user", "correct", "user@example.com");
        _clock.UtcNow += TimeSpan.FromMinutes(4);
        Assert.Null(await _store.ConsumeAsync<string>("reset:user", "wrong"));
        _clock.UtcNow += TimeSpan.FromMinutes(1);

        Assert.Null(await _store.ConsumeAsync<string>("reset:user", "correct"));
    }

    [Fact]
    public async Task Resend_ReplacesOldCodeAndResetsAttemptsAndExpiry()
    {
        await _store.SaveAsync("reset:user", "old", "old payload");
        for (var attempt = 0; attempt < 4; attempt++)
            await _store.ConsumeAsync<string>("reset:user", "wrong");
        _clock.UtcNow += TimeSpan.FromMinutes(4);

        await _store.SaveAsync("reset:user", "new", "new payload");
        _clock.UtcNow += TimeSpan.FromMinutes(2);
        Assert.Null(await _store.ConsumeAsync<string>("reset:user", "old"));
        Assert.Equal("new payload", await _store.ConsumeAsync<string>("reset:user", "new"));
    }

    [Fact]
    public async Task ConcurrentRequests_ConsumeCodeOnlyOnce()
    {
        await _store.SaveAsync("reset:user", "hash", "payload");

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => _store.ConsumeAsync<string>("reset:user", "hash"))));

        Assert.Single(results.Where(result => result is not null));
    }

    [Fact]
    public async Task RegistrationAndReset_KeepSeparateCodes()
    {
        await _store.SaveAsync("register:user", "registration hash", "registration");
        await _store.SaveAsync("reset:user", "reset hash", "reset");

        Assert.Equal("registration", await _store.ConsumeAsync<string>("register:user", "registration hash"));
        Assert.Equal("reset", await _store.ConsumeAsync<string>("reset:user", "reset hash"));
    }

    public void Dispose() => _cache.Dispose();

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
