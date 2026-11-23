using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class CoachReviewsTests
{
    [Fact]
    public async Task GetCoachReviews_AveragesOnlyThatCoachsSessions()
    {
        using var db = TestDbHelper.CreateDbContext();
        var sport = new Sport { Id = 1, Name = "Yoga", Description = "d", IsActive = true };
        var coachA = new Coach { Id = 1, UserId = 20, User = new User { Id = 20, Email = "a@t.com", FullName = "A", Role = "Coach", PasswordHash = "h", IsActive = true } };
        var coachB = new Coach { Id = 2, UserId = 21, User = new User { Id = 21, Email = "b@t.com", FullName = "B", Role = "Coach", PasswordHash = "h", IsActive = true } };
        var member = new Member { Id = 1, UserId = 30, User = new User { Id = 30, Email = "m@t.com", FullName = "M", Role = "Member", PasswordHash = "h", IsActive = true } };
        var cls = new SportClass { Id = 1, ClassName = "C", SportId = 1, Sport = sport };
        var policy = new CancellationPolicy { Id = 1 };
        var sessionA = new ClassSession { Id = 1, ClassId = 1, Class = cls, CoachId = 1, Coach = coachA, CancellationPolicyId = 1, CancellationPolicy = policy };
        var sessionB = new ClassSession { Id = 2, ClassId = 1, Class = cls, CoachId = 2, Coach = coachB, CancellationPolicyId = 1, CancellationPolicy = policy };
        db.AddRange(sport, coachA, coachB, member, cls, policy, sessionA, sessionB);
        int id = 1;
        foreach (var (session, rating) in new[] { (sessionA, 5), (sessionA, 4), (sessionB, 1) })
        {
            var reg = new ClassRegistration { Id = id, MemberId = 1, Member = member, ClassId = 1, Class = cls, SessionId = session.Id, Session = session };
            db.Add(reg);
            db.Add(new ClassReview { Id = id, RegistrationId = id, Registration = reg, Rating = rating, CreatedAt = DateTime.UtcNow });
            id++;
        }
        await db.SaveChangesAsync();

        var result = await new ClassService(db, TimeProvider.System, null!).GetCoachReviewsAsync(1, 1, 20);

        Assert.Equal(4.5, result.AverageRating);
        Assert.Equal(2, result.ReviewCount);
        Assert.Equal(2, result.Reviews.Items.Count);
    }

    [Fact]
    public async Task GetCoachReviews_UnknownCoach_Throws404()
    {
        using var db = TestDbHelper.CreateDbContext();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new ClassService(db, TimeProvider.System, null!).GetCoachReviewsAsync(99, 1, 20));
        Assert.Equal(404, ex.StatusCode);
    }
}
