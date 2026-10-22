using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsCenterAPI.Data;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;
using StackExchange.Redis;

namespace SportsCenterAPI.Tests;

// No SMTP or GroqCloud network calls. PostgreSQL and Redis are real, isolated containers.
public sealed class TestMail : IEmailService
{
    public ConcurrentDictionary<string, string> Codes { get; } = new();
    public Task SendEmailAsync(string to, string subject, string body, string? htmlBody = null)
    {
        Codes[to] = Regex.Match(body, @"\b\d{6}\b").Value;
        return Task.CompletedTask;
    }
}

public sealed class TestAiHandler : HttpMessageHandler
{
    public const string Completed = """
        {"status":"completed","output":[{"type":"reasoning"},{"type":"message","content":[
        {"type":"output_text","text":"Gợi ý bài tập"},{"type":"output_text","text":"Theo mục tiêu"}]}]}
        """;
    public string Body { get; set; } = Completed;
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public Exception? Error { get; set; }
    public string? LastRequest { get; private set; }
    public void Reset() { Body = Completed; Status = HttpStatusCode.OK; Error = null; LastRequest = null; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("https://api.groq.com/openai/v1/responses", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-only-fake-key", request.Headers.Authorization?.Parameter);
        LastRequest = await request.Content!.ReadAsStringAsync(ct);
        if (Error is not null) throw Error;
        return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
    }
}

public sealed class ApiFixture : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    public const string Password = "Test-only-123!";
    public const string MemberEmail = "member-a@example.test";
    public TestMail Mail { get; } = new();
    public TestAiHandler Ai { get; } = new();
    public int MemberA { get; private set; }
    public int MemberB { get; private set; }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Run tests/Run-Checks.ps1 first: {name} is missing.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = Required("SPORTSCENTER_TEST_POSTGRES"),
            ["ConnectionStrings:Redis"] = Required("SPORTSCENTER_TEST_REDIS"),
            ["Database:InitializeOnStartup"] = "false",
            ["OpenApi:Enabled"] = "true",
            ["Jwt:SecretKey"] = "test-only-signing-key-at-least-32-characters-long",
            ["Jwt:Issuer"] = "sportscenter-test", ["Jwt:Audience"] = "sportscenter-test",
            ["AI:ApiKey"] = "test-only-fake-key", ["AI:Model"] = "openai/gpt-oss-20b",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Mail);
            services.AddHttpClient<IAIService, AIService>().ConfigurePrimaryHttpMessageHandler(() => Ai);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // This connection is supplied only by the dedicated test-container runner.
        await db.Database.EnsureCreatedAsync();
        var hash = PasswordHelper.HashPassword(Password);
        User Account(string email, string role) => new()
            { Email = email, FullName = email, Role = role, PasswordHash = hash };
        var a = new Member { User = Account(MemberEmail, "Member"), FitnessGoal = "goal-A-private" };
        var b = new Member { User = Account("member-b@example.test", "Member"), FitnessGoal = "goal-B-private" };
        var coach = new Coach { User = Account("coach@example.test", "Coach") };
        db.AddRange(a, b, coach, Account("manager@example.test", "Manager"));
        var now = DateTime.UtcNow;
        var sportClass = new SportClass
        {
            ClassName = "schedule-A-private", Sport = new Sport { Name = "Test sport" }, Coach = coach,
            MaxCapacity = 20, StartDate = now, EndDate = now.AddDays(30)
        };
        var session = new ClassSession
        {
            Class = sportClass, Coach = coach, CancellationPolicyId = 1,
            StartsAt = now.AddDays(1), EndsAt = now.AddDays(1).AddHours(1), Capacity = 20
        };
        db.Add(new ClassRegistration { Member = a, Class = sportClass, Session = session });
        db.Add(new Attendance { Member = a, Class = sportClass });
        var package = new MembershipPackage { PackageName = "Public package", Price = 100, DurationInDays = 30 };
        db.AddRange(
            new MemberSubscription { Member = a, Package = package, Status = "Active", StartDate = now.AddDays(-1), EndDate = now.AddDays(29) },
            new MemberSubscription { Member = b, Package = package, Status = "B-private-status" });
        foreach (var (member, marker) in new[] { (a, "A"), (b, "B") })
            db.Add(new TrainingPlanExercise
            {
                TrainingPlan = new TrainingPlan { Member = member, Coach = member == a ? coach : null,
                    PlanName = $"Plan {marker}", StartDate = now, EndDate = now.AddDays(30) },
                Exercise = new Exercise { ExerciseName = $"exercise-{marker}-private" },
                Sets = 3, Reps = 10, Notes = $"notes-{marker}-private"
            });
        await db.SaveChangesAsync();
        MemberA = a.Id; MemberB = b.Id;
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    public async Task<HttpClient> Login(string email = MemberEmail)
    {
        var client = Client();
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return client;
    }
    public IDatabase Redis => Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
