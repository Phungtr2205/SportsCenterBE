using SportsCenterAPI.DTOs.Support;

namespace SportsCenterAPI.Services.Interface
{
    public interface ISupportTicketService
    {
        Task<SupportTicketResponse> CreateTicketAsync(int userId, CreateTicketRequest request);
        Task<IEnumerable<SupportTicketResponse>> GetMyTicketsAsync(int userId);
        Task<IEnumerable<SupportTicketResponse>> GetAllTicketsAsync(string? status);
        Task<SupportTicketResponse> ResolveTicketAsync(int ticketId, int receptionistId, ResolveTicketRequest request);
        Task<SupportTicketResponse> SetTicketImageAsync(int userId, int ticketId, IFormFile file, CancellationToken ct = default);
    }
}