using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Support;
using SportsCenterAPI.Services.Interface;
using System.Security.Claims;

namespace SportsCenterAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SupportTicketsController : ControllerBase
    {
        private readonly ISupportTicketService _ticketService;
        public SupportTicketsController(ISupportTicketService ticketService) { _ticketService = ticketService; }

        /// <summary>
        /// Gửi yêu cầu hỗ trợ đến trung tâm
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
        {
            var userIdString = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            var result = await _ticketService.CreateTicketAsync(int.Parse(userIdString), request);
            return Ok(result);
        }

        /// <summary>
        /// Xem các yêu cầu hỗ trợ của tôi
        /// </summary>
        [HttpGet("my-tickets")]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> GetMyTickets()
        {
            var userIdString = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            var result = await _ticketService.GetMyTicketsAsync(int.Parse(userIdString));
            return Ok(result);
        }

        /// <summary>
        /// Nhân viên xem danh sách yêu cầu hỗ trợ
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Receptionist,Manager")]
        public async Task<IActionResult> GetAllTickets([FromQuery] string? status)
        {
            var result = await _ticketService.GetAllTicketsAsync(status);
            return Ok(result);
        }

        /// <summary>
        /// Phản hồi và xử lý yêu cầu hỗ trợ
        /// </summary>
        [HttpPut("{id}/resolve")]
        [Authorize(Roles = "Receptionist,Manager")]
        public async Task<IActionResult> ResolveTicket(int id, [FromBody] ResolveTicketRequest request)
        {
            var userIdString = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            var result = await _ticketService.ResolveTicketAsync(id, int.Parse(userIdString), request);
            return Ok(result);
        }
    }
}