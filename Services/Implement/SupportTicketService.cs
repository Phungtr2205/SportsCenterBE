using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Support;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement
{
    public class SupportTicketService : ISupportTicketService
    {
        private readonly AppDbContext _context;
        private readonly IImageStorage _imageStorage;
        public SupportTicketService(AppDbContext context, IImageStorage imageStorage)
            { _context = context;
            _imageStorage = imageStorage;
            }

        public async Task<SupportTicketResponse> CreateTicketAsync(int userId, CreateTicketRequest request)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId)
                ?? throw new KeyNotFoundException("Tài khoản chưa có hồ sơ Hội viên!");

            var ticket = new SupportTicket
            {
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                MemberId = member.Id,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };
            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();
            return await GetTicketByIdAsync(ticket.Id);
        }

        public async Task<IEnumerable<SupportTicketResponse>> GetMyTicketsAsync(int userId)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId);
            if (member == null) return new List<SupportTicketResponse>();

            return await _context.SupportTickets
                .Include(t => t.Member).ThenInclude(m => m.User)
                .Include(t => t.HandledByUser)
                .Where(t => t.MemberId == member.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => MapToResponse(t)).ToListAsync();
        }

        public async Task<IEnumerable<SupportTicketResponse>> GetAllTicketsAsync(string? status)
        {
            var query = _context.SupportTickets
                .Include(t => t.Member).ThenInclude(m => m.User)
                .Include(t => t.HandledByUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status.ToLower() == status.ToLower());

            return await query.OrderByDescending(t => t.CreatedAt).Select(t => MapToResponse(t)).ToListAsync();
        }

        public async Task<SupportTicketResponse> ResolveTicketAsync(int ticketId, int receptionistId, ResolveTicketRequest request)
        {
            var ticket = await _context.SupportTickets
                .Include(t => t.Member).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(t => t.Id == ticketId)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu hỗ trợ");

            ticket.Status = request.Status;
            ticket.ResolutionNote = request.ResolutionNote;
            ticket.HandledByUserId = receptionistId;

            if (request.Status == "Resolved" || request.Status == "Closed")
                ticket.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetTicketByIdAsync(ticket.Id);
        }

        private async Task<SupportTicketResponse> GetTicketByIdAsync(int id)
        {
            var ticket = await _context.SupportTickets
                .Include(t => t.Member).ThenInclude(m => m.User).Include(t => t.HandledByUser)
                .FirstOrDefaultAsync(t => t.Id == id);
            return MapToResponse(ticket!);
        }

        private static SupportTicketResponse MapToResponse(SupportTicket t)
        {
            return new SupportTicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                MemberName = t.Member.User.FullName,
                HandledByUserName = t.HandledByUser?.FullName,
                ResolutionNote = t.ResolutionNote,
                CreatedAt = t.CreatedAt,
                ResolvedAt = t.ResolvedAt,
                ImageUrl = t.ImageUrl
            };
        }

        public async Task<SupportTicketResponse> SetTicketImageAsync(
            int userId,
            int ticketId,
            IFormFile file,
            CancellationToken ct = default)
            {
                var ticket = await _context.SupportTickets
                    .Include(t => t.Member).ThenInclude(m => m.User)
                    .Include(t => t.HandledByUser)
                    .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                    ?? throw new KeyNotFoundException("Không tìm thấy phiếu hỗ trợ.");

                // Chỉ cho phép chính hội viên tạo phiếu đó đính kèm ảnh khi phiếu chưa đóng
                if (ticket.Member.UserId != userId)
                    throw new UnauthorizedAccessException("Bạn không có quyền cập nhật ảnh cho phiếu hỗ trợ này.");

                if (ticket.Status == "Resolved" || ticket.Status == "Closed")
                    throw new InvalidOperationException("Không thể cập nhật ảnh cho phiếu hỗ trợ đã giải quyết hoặc đã đóng.");

                ticket.ImageUrl = await _imageStorage.UploadAsync(file, "support-tickets", ct);
                await _context.SaveChangesAsync(ct);

                return MapToResponse(ticket);
            }

    }
}