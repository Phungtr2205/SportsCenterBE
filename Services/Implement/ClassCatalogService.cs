using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.ClassCatalog;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class ClassCatalogService : IClassCatalogService
{
    private readonly AppDbContext _context;
    private readonly IImageStorage _imageStorage;

    public ClassCatalogService(AppDbContext context, IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    public async Task<List<SportListDTO>> GetSportsAsync(
        CancellationToken cancellationToken)
    {
        var sports = await _context.Sports
            .AsNoTracking()
            .Where(sport => sport.IsActive)
            .OrderBy(sport => sport.Id)
            .Select(sport => new SportListDTO
            {
                Id = sport.Id,
                Name = sport.Name,
                Description = sport.Description,
                ImageUrl = sport.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return sports;
    }

    public async Task<SportDTO> CreateSportAsync(
        SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken)
    {
        var sport = new Sport
        {
            Name = sportRequestDTO.Name.Trim(),
            Description = sportRequestDTO.Description?.Trim(),
            IsActive = sportRequestDTO.IsActive
        };

        _context.Sports.Add(sport);
        await _context.SaveChangesAsync(cancellationToken);

        return MapSportToDTO(sport);
    }

    public async Task<SportDTO> UpdateSportAsync(
        int id,
        SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken)
    {
        // Giữ việc kiểm tra lịch học và cập nhật trong cùng transaction.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var sport = await _context.Sports
            .SingleOrDefaultAsync(
                sport => sport.Id == id,
                cancellationToken);

        if (sport == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy bộ môn.");
        }

        if (!sportRequestDTO.IsActive)
        {
            var hasScheduledSessions = await _context.ClassSessions
                .AnyAsync(
                    session => session.Class.SportId == id
                        && session.Status == "Scheduled",
                    cancellationToken);

            if (hasScheduledSessions)
            {
                throw new BusinessException(
                    409,
                    "Bộ môn vẫn còn buổi học chưa hoàn thành/hủy.");
            }
        }

        sport.Name = sportRequestDTO.Name.Trim();
        sport.Description = sportRequestDTO.Description?.Trim();
        sport.IsActive = sportRequestDTO.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapSportToDTO(sport);
    }

    public async Task<SportDTO> SetSportImageAsync(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var sport = await _context.Sports.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new BusinessException(404, "Không tìm thấy bộ môn.");
        sport.ImageUrl = await _imageStorage.UploadAsync(file, "sports", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return MapSportToDTO(sport);
    }

    public async Task<List<CoachDTO>> GetCoachesAsync(
        CancellationToken cancellationToken)
    {
        var coaches = await _context.Coaches
            .AsNoTracking()
            .Where(coach =>
                coach.User.IsActive &&
                coach.User.Role == "Coach")
            .OrderBy(coach => coach.Id)
            .Select(coach => new CoachDTO
            {
                Id = coach.Id,
                FullName = coach.User.FullName,
                Specialization = coach.Specialization,
                Bio = coach.Bio,
                YearsOfExperience = coach.YearsOfExperience
            })
            .ToListAsync(cancellationToken);

        return coaches;
    }

    public async Task<CoachProfileDTO> UpdateCoachAsync(
        int id,
        CoachProfileRequestDTO coachProfileRequestDTO,
        CancellationToken cancellationToken)
    {
        var coach = await _context.Coaches
            .SingleOrDefaultAsync(
                coach => coach.Id == id,
                cancellationToken);

        if (coach == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy HLV.");
        }

        coach.Specialization = coachProfileRequestDTO.Specialization?.Trim();
        coach.Bio = coachProfileRequestDTO.Bio?.Trim();
        coach.YearsOfExperience = coachProfileRequestDTO.YearsOfExperience;

        await _context.SaveChangesAsync(cancellationToken);

        return new CoachProfileDTO
        {
            Id = coach.Id,
            Specialization = coach.Specialization,
            Bio = coach.Bio,
            YearsOfExperience = coach.YearsOfExperience
        };
    }

    private static SportDTO MapSportToDTO(Sport sport)
    {
        return new SportDTO
        {
            Id = sport.Id,
            Name = sport.Name,
            Description = sport.Description,
            ImageUrl = sport.ImageUrl,
            IsActive = sport.IsActive
        };
    }
}