using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SportsCenterAPI.DTOs.Auth;
using SportsCenterAPI.DTOs.Login;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Data;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Tests;

// Cần SQLite vì RenewToken dùng ExecuteUpdateAsync, EF InMemory không hỗ trợ.
public class AuthRefreshTokenTests : IDisposable
{
    private readonly SqliteTestDb _sqlite = new();
    private readonly AppDbContext _db;
    private readonly AuthService _service;

    public AuthRefreshTokenTests()
    {
        _db = _sqlite.CreateContext();
        _db.Users.Add(new User
        {
            Id = 100, Email = "member@test.com", FullName = "Member", Role = "Member",
            PasswordHash = PasswordHelper.HashPassword("Password@123"), IsActive = true
        });
        _db.SaveChanges();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = new string('k', 48),
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
        }).Build();

        _service = new AuthService(_db, config, null!, Mock.Of<IEmailService>(), Mock.Of<IGoogleTokenVerifier>());
    }

    public void Dispose()
    {
        _db.Dispose();
        _sqlite.Dispose();
    }

    private async Task<string> LoginAsync() =>
        (await _service.LoginAsync(new LoginRequestDTO { Email = "member@test.com", Password = "Password@123" })).RefreshTokenKey!;

    private Task<AuthResponse> RenewAsync(string key) =>
        _service.RenewToken(new RefreshTokenDTO { RefreshTokenKey = key });

    [Fact]
    public async Task Renew_ReturnsNewToken_AndNewTokenWorksNext()
    {
        var a = await LoginAsync();

        var b = (await RenewAsync(a)).RefreshTokenKey!;
        var c = (await RenewAsync(b)).RefreshTokenKey!;

        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
    }

    [Fact]
    public async Task Renew_RetryWithOldToken_WithinGrace_Succeeds()
    {
        var a = await LoginAsync();
        await RenewAsync(a); // phản hồi bị mất, client không nhận được token mới

        var retry = await RenewAsync(a);

        Assert.False(string.IsNullOrEmpty(retry.RefreshTokenKey));
    }

    [Fact]
    public async Task Renew_OldToken_AfterGrace_IsRejected()
    {
        var a = await LoginAsync();
        await RenewAsync(a);
        await _db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.RotatedAt, DateTime.UtcNow.AddSeconds(-60)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RenewAsync(a));
    }

    [Fact]
    public async Task Renew_RevokedToken_IsRejectedEvenWithinGrace()
    {
        var a = await LoginAsync();
        await RenewAsync(a);
        await _db.RefreshTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RenewAsync(a));
    }
}
