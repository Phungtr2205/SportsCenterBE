using SportsCenterAPI.DTOs.ClassCatalog;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class CoachReviewServiceTests
{
    private static async Task<(User memberUser, Member member, User coachUser, Coach coach)> SeedMemberAndCoachAsync(
        Data.AppDbContext db, int memberUserId = 10, int memberId = 1, int coachUserId = 20, int coachId = 1, bool coachActive = true)
    {
        var memberUser = new User
        {
            Id = memberUserId,
            FullName = $"Member {memberId}",
            Email = $"member{memberId}@test.com",
            PasswordHash = "h",
            Role = UserRoles.Member,
            IsActive = true
        };
        var member = new Member { Id = memberId, UserId = memberUserId, User = memberUser };

        var coachUser = new User
        {
            Id = coachUserId,
            FullName = $"Coach {coachId}",
            Email = $"coach{coachId}@test.com",
            PasswordHash = "h",
            Role = UserRoles.Coach,
            IsActive = coachActive
        };
        var coach = new Coach { Id = coachId, UserId = coachUserId, User = coachUser, Specialization = "Yoga", YearsOfExperience = 4 };

        db.Users.AddRange(memberUser, coachUser);
        db.Members.Add(member);
        db.Coaches.Add(coach);
        await db.SaveChangesAsync();

        return (memberUser, member, coachUser, coach);
    }

    [Fact]
    public async Task CreateAsync_MemberAttendedCompletedSession_CreatesReviewNotificationAndAudit()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (memberUser, member, coachUser, coach) = await SeedMemberAndCoachAsync(db);

        var sport = new Sport { Id = 1, Name = "Yoga", IsActive = true };
        var policy = new CancellationPolicy { Id = 1, Name = "2h", MinimumHoursBeforeStart = 2, IsActive = true };
        var sportClass = new SportClass
        {
            Id = 1,
            ClassName = "Yoga Flow",
            SportId = 1,
            CoachId = coach.Id,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(7),
            IsActive = true
        };
        var session = new ClassSession
        {
            Id = 100,
            ClassId = sportClass.Id,
            CoachId = coach.Id,
            CancellationPolicyId = policy.Id,
            StartsAt = DateTime.UtcNow.AddHours(-3),
            EndsAt = DateTime.UtcNow.AddHours(-1),
            Capacity = 20,
            Status = "Completed"
        };
        var registration = new ClassRegistration
        {
            Id = 200,
            MemberId = member.Id,
            ClassId = sportClass.Id,
            SessionId = session.Id,
            Status = "Completed"
        };
        var attendance = new Attendance
        {
            Id = 300,
            MemberId = member.Id,
            ClassId = sportClass.Id,
            SessionId = session.Id,
            Status = "Present"
        };

        db.Sports.Add(sport);
        db.CancellationPolicies.Add(policy);
        db.SportClasses.Add(sportClass);
        db.ClassSessions.Add(session);
        db.ClassRegistrations.Add(registration);
        db.Attendances.Add(attendance);
        await db.SaveChangesAsync();

        var service = new CoachReviewService(db, TimeProvider.System);
        var result = await service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest
        {
            Rating = 5,
            Comment = "  HLV hướng dẫn rất nhiệt tình!  "
        });

        Assert.Equal(5, result.Rating);
        Assert.Equal("HLV hướng dẫn rất nhiệt tình!", result.Comment);
        Assert.Single(db.CoachReviews);
        Assert.Single(db.Notifications.Where(n => n.UserId == coachUser.Id));
        Assert.Single(db.AuditLogs.Where(a => a.EntityName == nameof(CoachReview)));
    }

    [Fact]
    public async Task CreateAsync_MemberWithTrainingPlan_Succeeds()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (memberUser, member, _, coach) = await SeedMemberAndCoachAsync(db);

        db.TrainingPlans.Add(new TrainingPlan
        {
            Id = 1,
            PlanName = "Giáo án 1-1",
            Goal = "MuscleGain",
            MemberId = member.Id,
            CoachId = coach.Id,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(25),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = new CoachReviewService(db, TimeProvider.System);
        var result = await service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest
        {
            Rating = 4,
            Comment = "Giáo án rõ ràng"
        });

        Assert.Equal(4, result.Rating);
        Assert.Equal(coach.Id, result.CoachId);
    }

    [Fact]
    public async Task CreateAsync_MemberNeverTrainedWithCoach_ThrowsConflict409()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (memberUser, _, _, coach) = await SeedMemberAndCoachAsync(db);

        var service = new CoachReviewService(db, TimeProvider.System);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 5 }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_DuplicateReview_Throws409_AndUpdateMineSucceeds()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (memberUser, member, _, coach) = await SeedMemberAndCoachAsync(db);

        db.TrainingPlans.Add(new TrainingPlan
        {
            Id = 2,
            PlanName = "Plan",
            MemberId = member.Id,
            CoachId = coach.Id,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10)
        });
        await db.SaveChangesAsync();

        var service = new CoachReviewService(db, TimeProvider.System);
        await service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 4, Comment = "Tốt" });

        var duplicateEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 5 }));
        Assert.Equal(409, duplicateEx.StatusCode);

        var updated = await service.UpdateMineAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 5, Comment = "Rất tốt" });
        Assert.Equal(5, updated.Rating);
        Assert.Equal("Rất tốt", updated.Comment);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_InvalidRatingOrInactiveCoach_ThrowsExpectedErrors()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (memberUser, _, _, coach) = await SeedMemberAndCoachAsync(db, coachActive: false);

        var service = new CoachReviewService(db, TimeProvider.System);

        var badRating = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 6 }));
        Assert.Equal(400, badRating.StatusCode);

        var inactiveCoach = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(memberUser.Id, coach.Id, new CoachReviewRequest { Rating = 5 }));
        Assert.Equal(404, inactiveCoach.StatusCode);
    }

    [Fact]
    public async Task GetByCoachAsync_AndGetCoachesAsync_ReturnCorrectAverageAndCounts()
    {
        using var db = TestDbHelper.CreateDbContext();
        var (_, m1, _, coach) = await SeedMemberAndCoachAsync(db, memberUserId: 10, memberId: 1, coachUserId: 20, coachId: 1);

        var m2User = new User { Id = 11, FullName = "Member 2", Email = "m2@test.com", PasswordHash = "h", Role = UserRoles.Member, IsActive = true };
        var m2 = new Member { Id = 2, UserId = 11, User = m2User };
        db.Users.Add(m2User);
        db.Members.Add(m2);

        db.CoachReviews.AddRange(
            new CoachReview { Id = 1, CoachId = coach.Id, MemberId = m1.Id, Rating = 5, Comment = "Xuất sắc", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new CoachReview { Id = 2, CoachId = coach.Id, MemberId = m2.Id, Rating = 4, Comment = "Ổn", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var reviewService = new CoachReviewService(db, TimeProvider.System);
        var summary = await reviewService.GetByCoachAsync(coach.Id, page: 1, pageSize: 10);

        Assert.Equal(4.5, summary.AverageRating);
        Assert.Equal(2, summary.ReviewCount);
        Assert.Equal(1, summary.RatingCounts[5]);
        Assert.Equal(1, summary.RatingCounts[4]);
        Assert.Equal(0, summary.RatingCounts[1]);
        Assert.Equal("Member 2", summary.Items[0].MemberName);

        var catalogService = new ClassCatalogService(db, null!);
        var coaches = await catalogService.GetCoachesAsync(CancellationToken.None);
        Assert.Single(coaches);
        Assert.Equal(4.5, coaches[0].AverageRating);
        Assert.Equal(2, coaches[0].ReviewCount);
    }
}