using SportsCenterAPI.DTOs.ClassCatalog;

namespace SportsCenterAPI.Services.Interface;

public interface IClassCatalogService
{
    Task<List<SportListDTO>> GetSportsAsync(
        CancellationToken cancellationToken);

    Task<SportDTO> CreateSportAsync(
        SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken);

    Task<SportDTO> UpdateSportAsync(
        int id,
        SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken);

    Task<SportDTO> SetSportImageAsync(int id, IFormFile file, CancellationToken cancellationToken);

    Task<List<CoachDTO>> GetCoachesAsync(
        CancellationToken cancellationToken);

    Task<CoachProfileDTO> UpdateCoachAsync(
        int id,
        CoachProfileRequestDTO coachProfileRequestDTO,
        CancellationToken cancellationToken);
}