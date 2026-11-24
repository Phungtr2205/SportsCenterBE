using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.DTOs.Training;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class TrainingPlanServiceTests
{
    private const int CoachUserId = 1, OtherCoachUserId = 2;

    private static async Task<(Data.AppDbContext db, TrainingPlanService service, int planId)> SeedAsync()
    {
        var db = TestDbHelper.CreateDbContext();
        db.Users.AddRange(
            new User { Id = 1, FullName = "Coach A", Email = "a@test.com", Role = "Coach" },
            new User { Id = 2, FullName = "Coach B", Email = "b@test.com", Role = "Coach" },
            new User { Id = 3, FullName = "Member", Email = "m@test.com", Role = "Member" });
        db.Coaches.AddRange(new Coach { Id = 1, UserId = 1 }, new Coach { Id = 2, UserId = 2 });
        db.Members.Add(new Member { Id = 1, UserId = 3 });
        db.Exercises.AddRange(
            new Exercise { Id = 1, ExerciseName = "Squat" },
            new Exercise { Id = 2, ExerciseName = "Plank" });
        db.TrainingPlans.Add(new TrainingPlan
        {
            Id = 10,
            PlanName = "Old",
            Goal = "WeightLoss",
            MemberId = 1,
            CoachId = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Exercises = [new TrainingPlanExercise { ExerciseId = 1, Sets = 3, Reps = 10 }]
        });
        await db.SaveChangesAsync();
        return (db, new TrainingPlanService(db), 10);
    }

    private static CreateTrainingPlanDTO Request() => new()
    {
        PlanName = "New",
        Goal = "MuscleGain",
        MemberId = 1,
        StartDate = DateTime.UtcNow,
        EndDate = DateTime.UtcNow.AddDays(10),
        Exercises = [new CreatePlanExerciseDTO { ExerciseId = 2, Sets = 4, Reps = 12 }]
    };

    [Fact]
    public async Task UpdatePlanAsync_ReplacesFieldsAndExercises()
    {
        var (db, service, planId) = await SeedAsync();

        var result = await service.UpdatePlanAsync(CoachUserId, planId, Request());

        Assert.Equal("New", result.PlanName);
        var exercise = Assert.Single(result.Exercises);
        Assert.Equal("Plank", exercise.ExerciseName);
        Assert.Single(await db.TrainingPlanExercises.Where(e => e.TrainingPlanId == planId).ToListAsync());
    }

    [Fact]
    public async Task DeletePlanAsync_RemovesPlanAndExercises()
    {
        var (db, service, planId) = await SeedAsync();

        await service.DeletePlanAsync(CoachUserId, planId);

        Assert.False(await db.TrainingPlans.AnyAsync());
        Assert.False(await db.TrainingPlanExercises.AnyAsync());
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == 3));
    }

    [Fact]
    public async Task OtherCoach_CannotUpdateOrDelete()
    {
        var (db, service, planId) = await SeedAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdatePlanAsync(OtherCoachUserId, planId, Request()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeletePlanAsync(OtherCoachUserId, planId));
        Assert.True(await db.TrainingPlans.AnyAsync());
    }

    [Fact]
    public async Task UpdatePlanAsync_CannotMoveToAnotherMember()
    {
        var (_, service, planId) = await SeedAsync();
        var request = Request();
        request.MemberId = 99;

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePlanAsync(CoachUserId, planId, request));
    }

    [Fact]
    public async Task CreatePlanAsync_MatchesCompletedPlanWithoutConfirm_Throws409Warning()
    {
        var (db, service, planId) = await SeedAsync();
        await service.UpdatePlanStatusAsync(CoachUserId, planId, "Completed");

        var duplicateRequest = new CreateTrainingPlanDTO
        {
            PlanName = "Tên khác",
            Goal = "WeightLoss",
            MemberId = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            ConfirmRepeat = false,
            Exercises = [new CreatePlanExerciseDTO { ExerciseId = 1, Sets = 4, Reps = 10 }]
        };

        var check = await service.CheckDuplicateCompletedPlanAsync(CoachUserId, duplicateRequest);
        Assert.True(check.IsDuplicate);
        Assert.Contains("Hội viên đã thực hiện giáo án này", check.Message);

        var ex = await Assert.ThrowsAsync<DuplicateCompletedPlanException>(() =>
            service.CreatePlanAsync(CoachUserId, duplicateRequest));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("Hội viên đã thực hiện giáo án này", ex.Message);
        Assert.Single(ex.MatchedPlans);
    }

    [Fact]
    public async Task CreatePlanAsync_WithConfirmRepeatTrue_AllowsRepeatAndMarksWarning()
    {
        var (db, service, planId) = await SeedAsync();
        await service.UpdatePlanStatusAsync(CoachUserId, planId, "Completed");

        var created = await service.CreatePlanAsync(CoachUserId, new CreateTrainingPlanDTO
        {
            PlanName = "Old",
            Goal = "WeightLoss",
            MemberId = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            ConfirmRepeat = true,
            Exercises = [new CreatePlanExerciseDTO { ExerciseId = 1, Sets = 3, Reps = 10 }]
        });

        Assert.True(created.HasCompletedBefore);
        Assert.Equal(planId, created.PreviousCompletedPlanId);
        Assert.Contains("Hội viên đã thực hiện giáo án này", created.RepeatNotice);
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == 3 && n.Content.Contains("Hội viên đã thực hiện giáo án này")));
    }

    [Fact]
    public async Task RequestPlanChangeAsync_MemberNotifiesCoach_AndRejectsOtherMember()
    {
        var (db, service, planId) = await SeedAsync();
        db.Users.Add(new User { Id = 4, FullName = "Other Member", Email = "m2@test.com", Role = "Member" });
        db.Members.Add(new Member { Id = 2, UserId = 4 });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RequestPlanChangeAsync(4, planId, new RequestPlanChangeDTO { Reason = "Đổi giúp tôi" }));

        Assert.True(await service.RequestPlanChangeAsync(3, planId, new RequestPlanChangeDTO { Reason = "Muốn giảm cường độ bài Squat" }));
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == CoachUserId && n.Content.Contains("Muốn giảm cường độ bài Squat")));
    }
}