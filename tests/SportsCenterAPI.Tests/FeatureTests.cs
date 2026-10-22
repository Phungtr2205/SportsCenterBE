using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.AI;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;
using StackExchange.Redis;

namespace SportsCenterAPI.Tests;

// One fixture: tests run sequentially because they control the same fake provider.
public class FeatureTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private const string Workout = "/api/ai/workout-recommendations";
    private const string Chat = "/api/ai/chat";
    private static string Email() => $"test-{Guid.NewGuid():N}@example.test";
    private static string Key(string email) => $"sportscenter:otp:register:{email}";

    private async Task<string> SendOtp(HttpClient client, string email, string name = "Test member")
    {
        var response = await client.PostAsJsonAsync("/api/Auth/send-register-otp",
            new { email, password = ApiFixture.Password, fullName = name, fitnessGoal = "Endurance" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(@"^\d{6}$", app.Mail.Codes[email]);
        return app.Mail.Codes[email];
    }
    private static Task<HttpResponseMessage> Verify(HttpClient client, string email, string otp) =>
        client.PostAsJsonAsync("/api/Auth/verify-register-otp", new { email, otp });

    [Fact]
    public async Task SwaggerIncludesBothAiEndpoints()
    {
        using var client = app.Client();
        var json = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(json.GetProperty("paths").TryGetProperty(Workout, out _));
        Assert.True(json.GetProperty("paths").TryGetProperty(Chat, out _));
    }

    [Theory]
    [InlineData("/api/ai/chat")]
    [InlineData("/api/ai/workout-recommendations")]
    public async Task AnonymousCannotUseAi(string path)
    {
        using var client = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, new { question = "Lịch tập?" })).StatusCode);
    }

    [Fact]
    public async Task MemberWorkoutSendsOnlyOwnHistoryAndParsesMessagesAfterReasoning()
    {
        app.Ai.Reset();
        using var client = await app.Login();
        var response = await client.PostAsJsonAsync(Workout, new { level = "Beginner", daysPerWeek = 3 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Gợi ý bài tập\nTheo mục tiêu", result.GetProperty("data").GetProperty("answer").GetString());
        using var body = JsonDocument.Parse(app.Ai.LastRequest!);
        Assert.Equal("openai/gpt-oss-20b", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("low", body.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal(3000, body.RootElement.GetProperty("max_output_tokens").GetInt32());
        var context = body.RootElement.GetProperty("input").GetString()!;
        Assert.Contains("goal-A-private", context);
        Assert.Contains("exercise-A-private", context);
        Assert.DoesNotContain("B-private", context);
        Assert.DoesNotContain(ApiFixture.Password, context);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task MemberCannotRequestAnotherMembersWorkout()
    {
        app.Ai.Reset();
        using var client = await app.Login();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(Workout, new { memberId = app.MemberB })).StatusCode);
        Assert.Null(app.Ai.LastRequest);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task CoachCanOnlyRecommendForAssignedStudent(bool assigned, int expected)
    {
        app.Ai.Reset();
        using var client = await app.Login("coach@example.test");
        var response = await client.PostAsJsonAsync(Workout, new { memberId = assigned ? app.MemberA : app.MemberB });
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("manager@example.test", "/api/ai/workout-recommendations")]
    [InlineData("manager@example.test", "/api/ai/chat")]
    [InlineData("coach@example.test", "/api/ai/chat")]
    public async Task WrongRoleIsForbidden(string email, string path)
    {
        using var client = await app.Login(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, new { question = "Lịch tập?" })).StatusCode);
    }

    [Fact]
    public async Task CoachMustSelectMember()
    {
        using var client = await app.Login("coach@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Workout, new { })).StatusCode);
    }

    [Theory]
    [InlineData(0, "Beginner")]
    [InlineData(8, "Beginner")]
    [InlineData(3, "Unknown")]
    public async Task InvalidWorkoutIsRejected(int daysPerWeek, string level)
    {
        app.Ai.Reset();
        using var client = await app.Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Workout, new { daysPerWeek, level })).StatusCode);
        Assert.Null(app.Ai.LastRequest);
    }

    [Fact]
    public async Task ChatUsesOnlyOwnScheduleAndSubscription()
    {
        app.Ai.Reset();
        using var client = await app.Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Chat, new { question = "Gói và lịch của tôi?" })).StatusCode);
        using var request = JsonDocument.Parse(app.Ai.LastRequest!);
        var messages = request.RootElement.GetProperty("input");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Gói và lịch của tôi?", messages[1].GetProperty("content").GetString());
        using var context = JsonDocument.Parse(messages[0].GetProperty("content").GetString()!);
        Assert.Single(context.RootElement.GetProperty("subscriptions").EnumerateArray());
        Assert.Single(context.RootElement.GetProperty("schedule").EnumerateArray());
        Assert.True(context.RootElement.GetProperty("subscriptions")[0].GetProperty("IsValidNow").GetBoolean());
        Assert.Contains("schedule-A-private", context.RootElement.ToString());
        Assert.DoesNotContain("B-private", context.RootElement.ToString());
    }

    // Opt in with -LiveGroq: only synthetic members from the isolated database are sent.
    [Theory]
    [Trait("Category", "LiveGroq")]
    [InlineData("tại sao tập ngực bị đau ?", "đau")]
    [InlineData("Chỉ tôi kỹ thuật squat cho người mới bắt đầu.", "squat")]
    public async Task LiveChatAnswersExerciseQuestionDespiteCancelledSubscription(string question, string topic)
    {
        var answer = await AskLiveGroq(question);
        Assert.Contains(topic, answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ptprotraining11", Compact(answer));
        Assert.DoesNotContain("Cancelled", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "LiveGroq")]
    public async Task LiveChatStillUsesSubscriptionDataWhenAsked()
    {
        var answer = await AskLiveGroq("Gói PT Pro Training 1-1 của tôi còn hiệu lực không?");
        Assert.Contains("ptprotraining11", Compact(answer));
        Assert.True(answer.Contains("hủy", StringComparison.OrdinalIgnoreCase) ||
                    answer.Contains("Cancelled", StringComparison.OrdinalIgnoreCase),
            "The answer should explain that this synthetic subscription was cancelled.");
    }

    // Generated text may use non-breaking spaces or typographic hyphens in package names.
    private static string Compact(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private async Task<string> AskLiveGroq(string question)
    {
        var apiKey = Environment.GetEnvironmentVariable("SPORTSCENTER_TEST_GROQ_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey), "Use tests/Run-Checks.ps1 -LiveGroq with a configured Groq key.");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = new Member { User = new User
        {
            FullName = "Synthetic Groq test member", Email = Email(), Role = "Member",
            PasswordHash = PasswordHelper.HashPassword(ApiFixture.Password)
        }};
        var package = await db.MembershipPackages.FirstOrDefaultAsync(p => p.PackageName == "PT Pro Training 1-1")
            ?? new MembershipPackage { PackageName = "PT Pro Training 1-1", Price = 100, DurationInDays = 30 };
        db.MemberSubscriptions.Add(new MemberSubscription { Member = member, Package = package, Status = "Cancelled" });
        await db.SaveChangesAsync();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ApiKey"] = apiKey, ["AI:Model"] = "openai/gpt-oss-20b"
        }).Build();
        return await new AIService(db, http, config).ChatAsync(member.UserId,
            new ChatAiRequest { Question = question }, CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyQuestionIsRejected(string question)
    {
        using var client = await app.Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Chat, new { question })).StatusCode);
    }

    [Theory]
    [InlineData(429, 503)]
    [InlineData(401, 502)]
    [InlineData(500, 502)]
    public async Task ProviderFailureIsSanitized(int provider, int expected)
    {
        app.Ai.Reset(); app.Ai.Status = (HttpStatusCode)provider; app.Ai.Body = "provider-secret-detail";
        using var client = await app.Login();
        var response = await client.PostAsJsonAsync(Chat, new { question = "Lịch tập?" });
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.DoesNotContain("provider-secret-detail", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\"}]}]}")]
    public async Task InvalidOrIncompleteProviderResponseReturns502(string body)
    {
        app.Ai.Reset(); app.Ai.Body = body;
        using var client = await app.Login();
        Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsJsonAsync(Chat, new { question = "Lịch tập?" })).StatusCode);
    }

    [Theory]
    [InlineData(true, 504)]
    [InlineData(false, 503)]
    public async Task ProviderTimeoutAndNetworkFailure(bool timeout, int expected)
    {
        app.Ai.Reset(); app.Ai.Error = timeout ? new TaskCanceledException() : new HttpRequestException();
        using var client = await app.Login();
        Assert.Equal(expected, (int)(await client.PostAsJsonAsync(Chat, new { question = "Lịch tập?" })).StatusCode);
    }

    [Fact]
    public async Task MissingAiConfigReturns503BeforeNetworkCall()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actorId = await db.Users.Where(u => u.Email == ApiFixture.MemberEmail).Select(u => u.Id).SingleAsync();
        using var http = new HttpClient(new TestAiHandler());
        var service = new AIService(db, http, new ConfigurationBuilder().Build());
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.ChatAsync(actorId, new ChatAiRequest { Question = "Lịch tập?" }, default));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task RegistrationStoresOnlyHashesExpiresInFiveMinutesAndCannotReplay()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        var entries = await app.Redis.HashGetAllAsync(Key(email));
        var stored = string.Join(" ", entries.Select(x => x.Value.ToString()));
        Assert.DoesNotContain(ApiFixture.Password, stored);
        Assert.NotEqual(otp, (string?)await app.Redis.HashGetAsync(Key(email), "otp_hash"));
        Assert.InRange((await app.Redis.KeyTimeToLiveAsync(Key(email)))!.Value.TotalSeconds, 285, 300);
        Assert.Equal(HttpStatusCode.OK, (await Verify(client, email, otp)).StatusCode);
        Assert.False(await app.Redis.KeyExistsAsync(Key(email)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Verify(client, email, otp)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/login", new { email, password = ApiFixture.Password })).StatusCode);
    }

    [Fact]
    public async Task FiveWrongAttemptsInvalidateCodeWithoutExtendingTtl()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        var wrong = otp == "000000" ? "111111" : "000000";
        await app.Redis.KeyExpireAsync(Key(email), TimeSpan.FromSeconds(60));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Verify(client, email, wrong)).StatusCode);
            if (i < 4)
                Assert.InRange((await app.Redis.KeyTimeToLiveAsync(Key(email)))!.Value.TotalSeconds, 45, 60);
        }
        Assert.False(await app.Redis.KeyExistsAsync(Key(email)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Verify(client, email, otp)).StatusCode);
    }

    [Fact]
    public async Task ExpiredOtpCannotCreateAccount()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        await app.Redis.KeyExpireAsync(Key(email), TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);
        Assert.Equal(HttpStatusCode.BadRequest, (await Verify(client, email, otp)).StatusCode);
    }

    [Fact]
    public async Task ResendReplacesPendingRegistration()
    {
        using var client = app.Client(); var email = Email(); await SendOtp(client, email, "Old name");
        var otp = await SendOtp(client, email, "Latest name");
        Assert.Equal(HttpStatusCode.OK, (await Verify(client, email, otp)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Latest name", (await db.Users.SingleAsync(u => u.Email == email)).FullName);
    }

    [Fact]
    public async Task ConcurrentVerificationConsumesOtpExactlyOnce()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        var responses = await Task.WhenAll(Verify(client, email, otp), Verify(client, email, otp));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        using var scope = app.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task PendingOtpCanBeVerifiedByAnotherApiInstance()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        await using var second = new ApiFixture(); // Same isolated Redis/DB, independent service provider.
        using var secondClient = second.Client();
        Assert.Equal(HttpStatusCode.OK, (await Verify(secondClient, email, otp)).StatusCode);
    }

    [Fact]
    public async Task PasswordResetChangesPasswordAndRevokesRefreshToken()
    {
        using var client = app.Client(); var email = Email(); var otp = await SendOtp(client, email);
        var registration = await Verify(client, email, otp);
        var auth = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/request-reset-password", new { email })).StatusCode);
        var resetOtp = app.Mail.Codes[email];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/verify-reset-password", new
            { email, otp = resetOtp, newPassword = "Changed-test-456!", confirmPassword = "Changed-test-456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/Auth/login", new { email, password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/login", new { email, password = "Changed-test-456!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/Auth/refresh-token", new
            { refreshTokenKey = auth.GetProperty("refreshTokenKey").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Auth/verify-reset-password", new
            { email, otp = resetOtp, newPassword = "Changed-test-456!", confirmPassword = "Changed-test-456!" })).StatusCode);
    }

    [Fact]
    public async Task RedisUnavailableFailsExplicitlyWithoutInMemoryFallback()
    {
        var options = ConfigurationOptions.Parse("127.0.0.1:1,abortConnect=false,connectTimeout=100,connectRetry=0,asyncTimeout=100,syncTimeout=100");
        options.BacklogPolicy = BacklogPolicy.FailFast;
        using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var store = new RedisOtpStore(redis);
        var error = await Assert.ThrowsAsync<BusinessException>(() => store.SaveAsync("test:otp", "hash", "payload"));
        Assert.Equal(503, error.StatusCode);
    }
}
