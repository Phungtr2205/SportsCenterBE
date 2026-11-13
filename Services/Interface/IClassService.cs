using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.Models;

namespace SportsCenterAPI.Services.Interface;

public interface IClassService
{
    Task<PageResult<ClassDTO>> SearchClassesAsync(ClassSearch search, CancellationToken ct = default);
    Task<PageResult<ClassDTO>> GetManagedClassesAsync(int actorId, ClassSearch search, CancellationToken ct = default);
    Task<ClassDTO> GetClassAsync(int id, CancellationToken ct = default);
    Task<ClassDTO> SaveClassAsync(int actorId, int? id, ClassRequest request, CancellationToken ct = default);
    Task<ClassDTO> SetClassImageAsync(int actorId, int id, IFormFile file, CancellationToken ct = default);
    Task DeactivateClassAsync(int actorId, int id, CancellationToken ct = default);
    Task<PageResult<SessionDTO>> SearchSessionsAsync(ClassSearch search, int? classId = null, int? actorCoachId = null, CancellationToken ct = default);
    Task<SessionDTO> SaveSessionAsync(int actorId, int classId, int? id, SessionRequest request, CancellationToken ct = default);
    Task CancelSessionAsync(int actorId, int id, CancelRequest request, CancellationToken ct = default);
    Task CompleteSessionAsync(int actorId, int id, CancellationToken ct = default);
    Task<List<CancellationPolicy>> GetPoliciesAsync(int actorId, CancellationToken ct = default);
    Task<CancellationPolicy> SavePolicyAsync(int actorId, int? id, PolicyRequest request, CancellationToken ct = default);
    Task<RegistrationDTO> BookAsync(int actorId, int sessionId, int? memberId = null, CancellationToken ct = default);
    Task<PageResult<RegistrationDTO>> GetRegistrationsAsync(int actorId, int? memberId, int page, int pageSize, CancellationToken ct = default);
    Task<RegistrationDTO> CancelRegistrationAsync(int actorId, int id, CancelRequest request, CancellationToken ct = default);
    Task<List<RosterDTO>> GetRosterAsync(int actorId, int sessionId, CancellationToken ct = default);
    Task<AttendanceDTO> CheckInAsync(int actorId, int registrationId, CancellationToken ct = default);
    Task<ReviewDTO> ReviewAsync(int actorId, int registrationId, ReviewRequest request, CancellationToken ct = default);
    Task<PageResult<ReviewDTO>> GetReviewsAsync(int sessionId, int page, int pageSize, CancellationToken ct = default);
}
