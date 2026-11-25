using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Training;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public sealed class DuplicateCompletedPlanException(
    string message,
    IReadOnlyList<MatchedCompletedPlanDTO> matchedPlans) : Exception(message)
{
    public int StatusCode => 409;
    public IReadOnlyList<MatchedCompletedPlanDTO> MatchedPlans { get; } = matchedPlans;
}

public class TrainingPlanService(AppDbContext context) : ITrainingPlanService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Completed", "Cancelled"
    };

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

    public async Task<DuplicatePlanCheckResponseDTO> CheckDuplicateCompletedPlanAsync(
        int coachUserId, CreateTrainingPlanDTO request)
    {
        _ = await context.Coaches.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Chỉ huấn luyện viên mới được kiểm tra giáo án.");

        var exercises = request.Exercises ?? [];
        var matched = await FindMatchingCompletedPlansAsync(
            request.MemberId,
            request.PlanName,
            request.Goal,
            exercises.Select(e => e.ExerciseId),
            excludePlanId: null);

        if (matched.Count == 0)
            return new DuplicatePlanCheckResponseDTO(false, null, Array.Empty<MatchedCompletedPlanDTO>());

        return new DuplicatePlanCheckResponseDTO(
            true,
            $"Hội viên đã thực hiện giáo án này (\"{matched[0].PlanName}\"). Bạn có thể chỉnh sửa, tạo lại bằng AI hoặc xác nhận để hội viên tập lại.",
            matched);
    }

    public async Task<TrainingPlanResponseDTO> CreatePlanAsync(int coachUserId, CreateTrainingPlanDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.PlanName) || string.IsNullOrWhiteSpace(request.Goal))
            throw new ArgumentException("Vui lòng nhập đầy đủ tên giáo án và mục tiêu.");

        var coach = await context.Coaches
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Chỉ huấn luyện viên mới được tạo giáo án.");

        var member = await context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == request.MemberId)
            ?? throw new ArgumentException("Không tìm thấy học viên.");

        var (startUtc, endUtc) = NormalizeDateRange(request.StartDate, request.EndDate);
        var exercises = request.Exercises ?? [];

        var matched = await FindMatchingCompletedPlansAsync(
            request.MemberId,
            request.PlanName,
            request.Goal,
            exercises.Select(e => e.ExerciseId),
            excludePlanId: null);

        if (matched.Count > 0 && !request.ConfirmRepeat)
        {
            throw new DuplicateCompletedPlanException(
                $"Hội viên đã thực hiện giáo án này (\"{matched[0].PlanName}\"). Bạn có thể chỉnh sửa, tạo lại bằng AI hoặc xác nhận để hội viên tập lại.",
                matched);
        }

        await EnsureDefaultExercisesAsync();
        var planExercises = await BuildPlanExercisesAsync(exercises);

        var plan = new TrainingPlan
        {
            PlanName = request.PlanName.Trim(),
            Goal = request.Goal.Trim(),
            MemberId = request.MemberId,
            CoachId = coach.Id,
            StartDate = startUtc,
            EndDate = endUtc,
            Status = "Active",
            Exercises = planExercises
        };

        context.TrainingPlans.Add(plan);

        var repeatHint = matched.Count > 0
            ? $" Lưu ý: Hội viên đã thực hiện giáo án này trước đây (\"{matched[0].PlanName}\"). Bạn có thể tập lại hoặc gửi yêu cầu thay đổi cho HLV."
            : string.Empty;

        context.Notifications.Add(new Notification
        {
            UserId = member.UserId,
            Title = matched.Count > 0 ? "Giáo án tập luyện mới (Đã từng thực hiện)" : "Giáo án tập luyện mới",
            Content = $"HLV {coach.User.FullName} vừa tạo giáo án \"{plan.PlanName}\" cho bạn.{repeatHint}",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        return await GetPlanByIdAsync(plan.Id);
    }

    public async Task<TrainingPlanResponseDTO> UpdatePlanAsync(int coachUserId, int planId, CreateTrainingPlanDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.PlanName) || string.IsNullOrWhiteSpace(request.Goal))
            throw new ArgumentException("Vui lòng nhập đầy đủ tên giáo án và mục tiêu.");

        var coach = await context.Coaches
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Chỉ huấn luyện viên mới được chỉnh sửa giáo án.");

        var plan = await context.TrainingPlans
            .Include(p => p.Member)
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể chỉnh sửa giáo án do chính mình phụ trách.");

        if (request.MemberId != plan.MemberId)
            throw new ArgumentException("Không thể chuyển giáo án sang hội viên khác.");

        if (!string.Equals(plan.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(409, "Chỉ có thể chỉnh sửa giáo án đang áp dụng (Active).");

        var (startUtc, endUtc) = NormalizeDateRange(request.StartDate, request.EndDate);
        var exercises = request.Exercises ?? [];

        var matched = await FindMatchingCompletedPlansAsync(
            plan.MemberId,
            request.PlanName,
            request.Goal,
            exercises.Select(e => e.ExerciseId),
            excludePlanId: plan.Id);

        if (matched.Count > 0 && !request.ConfirmRepeat)
        {
            throw new DuplicateCompletedPlanException(
                $"Hội viên đã thực hiện giáo án này (\"{matched[0].PlanName}\"). Bạn có thể chỉnh sửa lại hoặc xác nhận để hội viên tập lại.",
                matched);
        }

        await EnsureDefaultExercisesAsync();
        var updatedExercises = await BuildPlanExercisesAsync(exercises);

        plan.PlanName = request.PlanName.Trim();
        plan.Goal = request.Goal.Trim();
        plan.StartDate = startUtc;
        plan.EndDate = endUtc;

        context.TrainingPlanExercises.RemoveRange(plan.Exercises);
        plan.Exercises.Clear();
        foreach (var item in updatedExercises)
            plan.Exercises.Add(item);

        var repeatHint = matched.Count > 0
            ? $" Lưu ý: Hội viên đã thực hiện giáo án này trước đây (\"{matched[0].PlanName}\")."
            : string.Empty;

        context.Notifications.Add(new Notification
        {
            UserId = plan.Member.UserId,
            Title = "Giáo án tập luyện đã được cập nhật",
            Content = $"HLV {coach.User.FullName} đã cập nhật giáo án \"{plan.PlanName}\" cho bạn.{repeatHint}",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        return await GetPlanByIdAsync(plan.Id);
    }

    public async Task DeletePlanAsync(int coachUserId, int planId)
    {
        var coach = await context.Coaches
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Chỉ huấn luyện viên mới được xóa giáo án.");

        var plan = await context.TrainingPlans
            .Include(p => p.Member)
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể xóa giáo án do chính mình phụ trách.");

        context.TrainingPlanExercises.RemoveRange(plan.Exercises);
        context.TrainingPlans.Remove(plan);

        context.Notifications.Add(new Notification
        {
            UserId = plan.Member.UserId,
            Title = "Giáo án tập luyện đã bị xóa",
            Content = $"HLV {coach.User.FullName} đã xóa giáo án \"{plan.PlanName}\".",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<TrainingPlanResponseDTO>> GetMemberPlansAsync(int actorUserId, int memberId)
    {
        // Hội viên chỉ xem giáo án của mình; Coach, lễ tân, quản lý xem được mọi hội viên.
        var actorRole = await context.Users
            .Where(u => u.Id == actorUserId && u.IsActive)
            .Select(u => u.Role)
            .SingleOrDefaultAsync()
            ?? throw new BusinessException(401, "Vui lòng đăng nhập.");

        if (actorRole == UserRoles.Member &&
            !await context.Members.AnyAsync(m => m.Id == memberId && m.UserId == actorUserId))
        {
            throw new BusinessException(403, "Bạn chỉ được xem giáo án của chính mình.");
        }

        var allPlans = await context.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Member).ThenInclude(m => m.User)
            .Include(x => x.Coach).ThenInclude(c => c!.User)
            .Include(x => x.Exercises).ThenInclude(e => e.Exercise)
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        var completedPlans = allPlans
            .Where(p => string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return allPlans.Select(p => MapToDto(p, completedPlans)).ToList();
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
        var coach = await context.Coaches
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == coachUserId)
            ?? throw new UnauthorizedAccessException("Tài khoản không phải là huấn luyện viên.");

        var normalizedStatus = AllowedStatuses.FirstOrDefault(s =>
            string.Equals(s, status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Trạng thái giáo án chỉ chấp nhận: Active, Completed, Cancelled.");

        var plan = await context.TrainingPlans
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.CoachId != coach.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể cập nhật giáo án do chính mình phụ trách.");

        plan.Status = normalizedStatus;

        if (normalizedStatus == "Completed")
        {
            context.Notifications.Add(new Notification
            {
                UserId = plan.Member.UserId,
                Title = "Hoàn thành giáo án tập luyện",
                Content = $"HLV {coach.User.FullName} đã đánh dấu hoàn thành giáo án \"{plan.PlanName}\".",
                SentAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RequestPlanChangeAsync(int memberUserId, int planId, RequestPlanChangeDTO request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Vui lòng nhập nội dung bạn muốn thay đổi.");

        var member = await context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == memberUserId)
            ?? throw new UnauthorizedAccessException("Không tìm thấy hồ sơ hội viên.");

        var plan = await context.TrainingPlans
            .Include(p => p.Coach)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new ArgumentException("Không tìm thấy giáo án.");

        if (plan.MemberId != member.Id)
            throw new UnauthorizedAccessException("Bạn chỉ có thể gửi yêu cầu cho giáo án của chính mình.");

        if (!string.Equals(plan.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(409, "Chỉ có thể yêu cầu điều chỉnh giáo án đang áp dụng.");

        if (plan.Coach == null)
            throw new BusinessException(409, "Giáo án này chưa có huấn luyện viên phụ trách.");

        context.Notifications.Add(new Notification
        {
            UserId = plan.Coach.UserId,
            Title = "Yêu cầu điều chỉnh giáo án",
            Content = $"Hội viên {member.User.FullName} muốn thay đổi giáo án \"{plan.PlanName}\" (#{plan.Id}): {request.Reason.Trim()}",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<TrainingPlanResponseDTO> CompletePlanByMemberAsync(int memberUserId, int planId)
    {
        var member = await context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == memberUserId)
            ?? throw new BusinessException(404, "Không tìm thấy hồ sơ hội viên.");

        var plan = await context.TrainingPlans
            .Include(p => p.Coach)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new BusinessException(404, "Không tìm thấy giáo án.");

        if (plan.MemberId != member.Id)
            throw new BusinessException(403, "Bạn chỉ có thể xác nhận giáo án của chính mình.");

        // Bấm lại khi đã hoàn thành thì trả kết quả hiện tại, không báo lỗi.
        if (string.Equals(plan.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return await GetPlanByIdAsync(plan.Id);

        if (!string.Equals(plan.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(409, "Chỉ đánh dấu đã tập cho giáo án đang áp dụng.");

        if (plan.StartDate > DateTime.UtcNow)
            throw new BusinessException(409, "Giáo án chưa đến ngày bắt đầu.");

        plan.Status = "Completed";

        if (plan.Coach != null)
        {
            context.Notifications.Add(new Notification
            {
                UserId = plan.Coach.UserId,
                Title = "Hội viên đã tập xong giáo án",
                Content = $"Hội viên {member.User.FullName} đã xác nhận tập xong giáo án \"{plan.PlanName}\" (#{plan.Id}).",
                SentAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return await GetPlanByIdAsync(plan.Id);
    }

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

    private async Task<List<MatchedCompletedPlanDTO>> FindMatchingCompletedPlansAsync(
        int memberId,
        string planName,
        string? goal,
        IEnumerable<int> exerciseIds,
        int? excludePlanId)
    {
        var completed = await context.TrainingPlans
            .AsNoTracking()
            .Include(p => p.Exercises)
            .Where(p => p.MemberId == memberId
                && p.Status == "Completed"
                && (!excludePlanId.HasValue || p.Id != excludePlanId.Value))
            .OrderByDescending(p => p.EndDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        var normalizedExerciseIds = exerciseIds.Distinct().OrderBy(id => id).ToList();

        return completed
            .Where(p => IsSamePlan(p, planName, goal, normalizedExerciseIds))
            .Select(p => new MatchedCompletedPlanDTO(
                p.Id,
                p.PlanName,
                p.Goal ?? string.Empty,
                p.StartDate,
                p.EndDate))
            .ToList();
    }

    private static bool IsSamePlan(
        TrainingPlan completedPlan,
        string planName,
        string? goal,
        IReadOnlyList<int> normalizedExerciseIds)
    {
        var sameNameAndGoal =
            !string.IsNullOrWhiteSpace(planName) &&
            string.Equals(completedPlan.PlanName.Trim(), planName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals((completedPlan.Goal ?? string.Empty).Trim(), (goal ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        if (sameNameAndGoal) return true;

        if (normalizedExerciseIds.Count == 0) return false;

        var completedExerciseIds = completedPlan.Exercises
            .Select(e => e.ExerciseId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        return completedExerciseIds.SequenceEqual(normalizedExerciseIds);
    }

    private async Task<List<TrainingPlanExercise>> BuildPlanExercisesAsync(IEnumerable<CreatePlanExerciseDTO> exercises)
    {
        var planExercises = new List<TrainingPlanExercise>();
        foreach (var e in exercises)
        {
            var existingExercise = await context.Exercises
                .FirstOrDefaultAsync(ex => ex.Id == e.ExerciseId);

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

    private static (DateTime StartUtc, DateTime EndUtc) NormalizeDateRange(DateTime start, DateTime end)
    {
        var startUtc = start.Kind == DateTimeKind.Utc
            ? start
            : start.Kind == DateTimeKind.Local
                ? start.ToUniversalTime()
                : DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var endUtc = end.Kind == DateTimeKind.Utc
            ? end
            : end.Kind == DateTimeKind.Local
                ? end.ToUniversalTime()
                : DateTime.SpecifyKind(end, DateTimeKind.Utc);

        if (endUtc < startUtc)
            throw new ArgumentException("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");

        return (startUtc, endUtc);
    }

    private async Task<TrainingPlanResponseDTO> GetPlanByIdAsync(int id)
    {
        var p = await context.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Member).ThenInclude(m => m.User)
            .Include(x => x.Coach).ThenInclude(c => c!.User)
            .Include(x => x.Exercises).ThenInclude(e => e.Exercise)
            .FirstAsync(x => x.Id == id);

        var priorCompleted = await context.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Exercises)
            .Where(x => x.MemberId == p.MemberId && x.Status == "Completed" && x.Id != p.Id)
            .OrderByDescending(x => x.EndDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return MapToDto(p, priorCompleted);
    }

    private static TrainingPlanResponseDTO MapToDto(TrainingPlan p, IReadOnlyList<TrainingPlan> completedPlans)
    {
        var pExerciseIds = p.Exercises.Select(e => e.ExerciseId).Distinct().OrderBy(id => id).ToList();
        var matchedPrev = completedPlans.FirstOrDefault(prev =>
            prev.Id != p.Id &&
            (prev.EndDate <= p.StartDate || prev.Id < p.Id) &&
            IsSamePlan(prev, p.PlanName, p.Goal, pExerciseIds));

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
            HasCompletedBefore = matchedPrev != null,
            PreviousCompletedPlanId = matchedPrev?.Id,
            RepeatNotice = matchedPrev != null
                ? $"Hội viên đã thực hiện giáo án này (\"{matchedPrev.PlanName}\")."
                : null,
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