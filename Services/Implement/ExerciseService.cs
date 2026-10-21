using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Exercises;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class ExerciseService(AppDbContext context) : IExerciseService
{
    private async Task EnsureDefaultExercisesAsync(CancellationToken ct)
    {
        if (!await context.Exercises.AnyAsync(ct))
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
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<ExerciseResponseDTO>> GetExercisesAsync(string? muscleGroup = null, string? search = null, CancellationToken ct = default)
    {
        await EnsureDefaultExercisesAsync(ct);

        var query = context.Exercises.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(muscleGroup))
        {
            var mg = muscleGroup.Trim().ToLower();
            query = query.Where(e => e.MuscleGroup != null && e.MuscleGroup.ToLower() == mg);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(e => e.ExerciseName.ToLower().Contains(keyword) || (e.Description != null && e.Description.ToLower().Contains(keyword)));
        }

        return await query
            .OrderBy(e => e.Id)
            .Select(e => new ExerciseResponseDTO
            {
                Id = e.Id,
                ExerciseName = e.ExerciseName,
                MuscleGroup = e.MuscleGroup,
                Description = e.Description,
                VideoUrl = e.VideoUrl
            })
            .ToListAsync(ct);
    }

    public async Task<ExerciseResponseDTO> GetExerciseByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await context.Exercises.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException(404, "Không tìm thấy bài tập.");

        return new ExerciseResponseDTO
        {
            Id = e.Id,
            ExerciseName = e.ExerciseName,
            MuscleGroup = e.MuscleGroup,
            Description = e.Description,
            VideoUrl = e.VideoUrl
        };
    }

    public async Task<ExerciseResponseDTO> CreateExerciseAsync(CreateExerciseDTO request, CancellationToken ct = default)
    {
        var name = request.ExerciseName.Trim();
        bool isExisted = await context.Exercises.AnyAsync(e => e.ExerciseName.ToLower() == name.ToLower(), ct);
        if (isExisted)
        {
            throw new BusinessException(409, $"Bài tập \"{name}\" đã tồn tại trong kho.");
        }

        var exercise = new Exercise
        {
            ExerciseName = name,
            MuscleGroup = string.IsNullOrWhiteSpace(request.MuscleGroup) ? "General" : request.MuscleGroup.Trim(),
            Description = request.Description?.Trim(),
            VideoUrl = request.VideoUrl?.Trim()
        };

        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(ct);

        return new ExerciseResponseDTO
        {
            Id = exercise.Id,
            ExerciseName = exercise.ExerciseName,
            MuscleGroup = exercise.MuscleGroup,
            Description = exercise.Description,
            VideoUrl = exercise.VideoUrl
        };
    }

    public async Task<ExerciseResponseDTO> UpdateExerciseAsync(int id, UpdateExerciseDTO request, CancellationToken ct = default)
    {
        var exercise = await context.Exercises.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new BusinessException(404, "Không tìm thấy bài tập để cập nhật.");

        var name = request.ExerciseName.Trim();
        bool isDuplicate = await context.Exercises.AnyAsync(e => e.Id != id && e.ExerciseName.ToLower() == name.ToLower(), ct);
        if (isDuplicate)
        {
            throw new BusinessException(409, $"Đã có bài tập khác mang tên \"{name}\".");
        }

        exercise.ExerciseName = name;
        exercise.MuscleGroup = string.IsNullOrWhiteSpace(request.MuscleGroup) ? "General" : request.MuscleGroup.Trim();
        exercise.Description = request.Description?.Trim();
        exercise.VideoUrl = request.VideoUrl?.Trim();

        await context.SaveChangesAsync(ct);

        return new ExerciseResponseDTO
        {
            Id = exercise.Id,
            ExerciseName = exercise.ExerciseName,
            MuscleGroup = exercise.MuscleGroup,
            Description = exercise.Description,
            VideoUrl = exercise.VideoUrl
        };
    }

    public async Task<bool> DeleteExerciseAsync(int id, CancellationToken ct = default)
    {
        var exercise = await context.Exercises.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new BusinessException(404, "Không tìm thấy bài tập.");

        // Kiểm tra an toàn: nếu bài tập đã được gắn trong giáo án nào thì không cho xóa
        bool isUsedInPlan = await context.TrainingPlanExercises.AnyAsync(tpe => tpe.ExerciseId == id, ct);
        if (isUsedInPlan)
        {
            throw new BusinessException(409, "Bài tập này đang được sử dụng trong các giáo án học viên, không thể xóa.");
        }

        context.Exercises.Remove(exercise);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IEnumerable<string>> GetMuscleGroupsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultExercisesAsync(ct);

        var groups = await context.Exercises
            .AsNoTracking()
            .Where(e => !string.IsNullOrEmpty(e.MuscleGroup))
            .Select(e => e.MuscleGroup!)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync(ct);

        return groups;
    }
}