using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.Models;
using SportsCenterAPI.DTOs.Training;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class TrainingPlanService(AppDbContext context) : ITrainingPlanService
{
    private async Task EnsureDefaultExercisesAsync()
    {
        if (!await context.Exercises.AnyAsync())
        {
            context.Exercises.AddRange(
                new Exercise { ExerciseName = "Squat (Gánh tạ đùi)", MuscleGroup = "Legs", Description = "Phát triển cơ đùi trước và cơ mông." },
                new Exercise { ExerciseName = "Bench Press (Đẩy ngực)", MuscleGroup = "Chest", Description = "Phát triển cơ ngực, vai trước và tay sau." },
                new Exercise { ExerciseName = "Deadlift (Kéo lưng)", MuscleGroup = "Back", Description = "Bài tập toàn thân phát triển chuỗi cơ sau." },
                new Exercise { ExerciseName = "Plank (Giữ cơ bụng)", MuscleGroup = "Core", Description = "Tăng sức bền cơ trọng tâm (Core)." },
                new Exercise { ExerciseName = "Pull-up (Kéo xà đơn)", MuscleGroup = "Back", Description = "Phát triển cơ xô và tay trước." },
                new Exercise { ExerciseName = "Shoulder Press (Đẩy vai)", MuscleGroup = "Arms", Description = "Phát triển cơ vai săn chắc." },
                new Exercise { ExerciseName = "Lunges (Chùng chân)", MuscleGroup = "Legs", Description = "Giữ thăng bằng và săn chắc cơ đùi, mông." },
                new Exercise { ExerciseName = "HIIT Cardio (Chạy biến tốc)", MuscleGroup = "Cardio", Description = "Đốt mỡ cường độ cao và tăng sức bền tim mạch." }
            );
            await context.SaveChangesAsync();
        }
    }

    public async Task<TrainingPlanResponseDTO> CreatePlanAsync(int coachUserId, CreateTrainingPlanDTO request)
    {
        var coach = await context.Coaches
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Chỉ huấn luyện viên mới được tạo giáo án.");

        var member = await context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == request.MemberId)
            ?? throw new ArgumentException("Không tìm thấy học viên.");

        var planExercises = await BuildPlanExercisesAsync(request.Exercises);

        var plan = new TrainingPlan
        {
            PlanName = request.PlanName.Trim(),
            Goal = request.Goal,
            MemberId = request.MemberId,
            CoachId = coach.Id,
            StartDate = ToUtc(request.StartDate),
            EndDate = ToUtc(request.EndDate),
            Status = "Active",
            Exercises = planExercises
        };

        context.TrainingPlans.Add(plan);

        // Tự động gửi thông báo cho Học viên
        context.Notifications.Add(new Notification
        {
            UserId = member.UserId,
            Title = "Giáo án tập luyện mới",
            Content = $"HLV {coach.User.FullName} vừa tạo giáo án \"{plan.PlanName}\" cho bạn.",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        return await GetPlanByIdAsync(plan.Id);
    }

    public async Task<IEnumerable<TrainingPlanResponseDTO>> GetMemberPlansAsync(int memberId)
    {
        var plans = await context.TrainingPlans
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();

        var result = new List<TrainingPlanResponseDTO>();
        foreach (var id in plans)
        {
            result.Add(await GetPlanByIdAsync(id));
        }
        return result;
    }

    public async Task<bool> UpdateExerciseResultAsync(int coachUserId, int planExerciseId, UpdateExerciseResultDTO request)
    {
        var coach = await context.Coaches.FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Tài khoản không phải là huấn luyện viên.");

        var exercise = await context.TrainingPlanExercises
            .Include(e => e.Exercise)
            .Include(e => e.TrainingPlan)
                .ThenInclude(tp => tp.Member)
            .FirstOrDefaultAsync(e => e.Id == planExerciseId)
            ?? throw new ArgumentException("Không tìm thấy bài tập trong giáo án.");

        if (exercise.TrainingPlan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể cập nhật giáo án do chính mình phụ trách.");

        exercise.Notes = request.Notes.Trim();

        // Tự động gửi thông báo tiến độ cho Học viên
        context.Notifications.Add(new Notification
        {
            UserId = exercise.TrainingPlan.Member.UserId,
            Title = "Cập nhật đánh giá bài tập",
            Content = $"HLV đã nhận xét bài tập \"{exercise.Exercise?.ExerciseName}\" trong giáo án \"{exercise.TrainingPlan.PlanName}\": {exercise.Notes}",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePlanStatusAsync(int coachUserId, int planId, string status)
    {
        var coach = await context.Coaches.FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Tài khoản không phải là huấn luyện viên.");

        var plan = await context.TrainingPlans
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể cập nhật giáo án do chính mình phụ trách.");

        plan.Status = status.Trim();
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<TrainingPlanResponseDTO> UpdatePlanAsync(int coachUserId, int planId, CreateTrainingPlanDTO request)
    {
        var plan = await GetOwnedPlanAsync(coachUserId, planId);

        if (plan.MemberId != request.MemberId)
            throw new ArgumentException("Không thể chuyển giáo án sang học viên khác.");

        var planExercises = await BuildPlanExercisesAsync(request.Exercises);

        // Thay toàn bộ danh sách bài tập cũ bằng danh sách mới
        context.TrainingPlanExercises.RemoveRange(plan.Exercises);
        plan.PlanName = request.PlanName.Trim();
        plan.Goal = request.Goal;
        plan.StartDate = ToUtc(request.StartDate);
        plan.EndDate = ToUtc(request.EndDate);
        plan.Exercises = planExercises;

        context.Notifications.Add(new Notification
        {
            UserId = plan.Member.UserId,
            Title = "Giáo án được cập nhật",
            Content = $"Giáo án \"{plan.PlanName}\" của bạn vừa được huấn luyện viên cập nhật.",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return await GetPlanByIdAsync(plan.Id);
    }

    public async Task DeletePlanAsync(int coachUserId, int planId)
    {
        var plan = await GetOwnedPlanAsync(coachUserId, planId);

        // Khóa ngoại là Restrict nên phải xóa bài tập trước khi xóa giáo án
        context.TrainingPlanExercises.RemoveRange(plan.Exercises);
        context.TrainingPlans.Remove(plan);

        context.Notifications.Add(new Notification
        {
            UserId = plan.Member.UserId,
            Title = "Giáo án đã bị xóa",
            Content = $"Huấn luyện viên đã xóa giáo án \"{plan.PlanName}\" của bạn.",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private async Task<TrainingPlan> GetOwnedPlanAsync(int coachUserId, int planId)
    {
        var coach = await context.Coaches.FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Tài khoản không phải là huấn luyện viên.");

        var plan = await context.TrainingPlans
            .Include(p => p.Member)
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể sửa hoặc xóa giáo án do chính mình phụ trách.");

        return plan;
    }

    private async Task<List<TrainingPlanExercise>> BuildPlanExercisesAsync(List<CreatePlanExerciseDTO> exercises)
    {
        // Đảm bảo danh mục bài tập mẫu luôn có sẵn trong DB
        await EnsureDefaultExercisesAsync();

        var planExercises = new List<TrainingPlanExercise>();
        foreach (var e in exercises)
        {
            var existingExercise = await context.Exercises
                .FirstOrDefaultAsync(ex => ex.Id == e.ExerciseId);

            // Nếu HLV nhập một mã bài tập chưa có, tự động tạo bài tập mới để không bao giờ lỗi khóa ngoại
            if (existingExercise == null)
            {
                existingExercise = new Exercise
                {
                    ExerciseName = !string.IsNullOrWhiteSpace(e.Notes)
                        ? e.Notes.Trim()
                        : $"Bài tập #{e.ExerciseId}",
                    MuscleGroup = "General",
                    Description = "Bài tập huấn luyện cá nhân"
                };
                context.Exercises.Add(existingExercise);
                await context.SaveChangesAsync();
            }

            planExercises.Add(new TrainingPlanExercise
            {
                ExerciseId = existingExercise.Id,
                Sets = e.Sets,
                Reps = e.Reps,
                DurationInMinutes = e.DurationInMinutes,
                Notes = e.Notes
            });
        }

        return planExercises;
    }

    // Chuyển ngày về chuẩn UTC cho PostgreSQL
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task<IEnumerable<object>> GetExercisesCatalogAsync()
    {
        await EnsureDefaultExercisesAsync();

        return await context.Exercises
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.ExerciseName,
                e.MuscleGroup,
                e.Description,
                e.VideoUrl
            })
            .ToListAsync();
    }

    private async Task<TrainingPlanResponseDTO> GetPlanByIdAsync(int id)
    {
        var p = await context.TrainingPlans
            .Include(x => x.Member).ThenInclude(m => m.User)
            .Include(x => x.Coach).ThenInclude(c => c!.User)
            .Include(x => x.Exercises).ThenInclude(e => e.Exercise)
            .FirstAsync(x => x.Id == id);

        return new TrainingPlanResponseDTO
        {
            Id = p.Id,
            PlanName = p.PlanName,
            Goal = p.Goal ?? "",
            MemberName = p.Member.User.FullName,
            CoachName = p.Coach?.User.FullName ?? "N/A",
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status,
            Exercises = p.Exercises.Select(e => new PlanExerciseResponseDTO
            {
                Id = e.Id,
                ExerciseName = e.Exercise?.ExerciseName ?? "Unknown",
                Sets = e.Sets,
                Reps = e.Reps,
                DurationInMinutes = e.DurationInMinutes,
                Notes = e.Notes
            }).ToList()
        };
    }
}