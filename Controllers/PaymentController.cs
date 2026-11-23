using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS.Exceptions;
using PayOS.Models.Webhooks;
using QuestPDF.Fluent;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models.DTOs.Payments;
using SportsCenterAPI.Services.Interface;
using System.Globalization;
using System.Security.Claims;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Member,Receptionist,Manager")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly AppDbContext _context;

    public PaymentsController(
        IPaymentService paymentService,
        AppDbContext context)
    {
        _paymentService = paymentService;
        _context = context;
    }

    // GET /api/payments
    // Hội viên chỉ xem các thanh toán của mình.
    /// <summary>
    /// Xem lịch sử thanh toán trong phạm vi được phép
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<List<PaymentDTO>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _paymentService.GetAllAsync(ActorId());

        return StatusCode(response.StatusCode, response);
    }

    // GET /api/payments/{id}
    /// <summary>
    /// Xem chi tiết giao dịch thanh toán
    /// </summary>
    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentDTO>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await _paymentService.GetByIdAsync(
            id,
            ActorId());

        return StatusCode(response.StatusCode, response);
    }

    // POST /api/payments
    // Tạo hoặc lấy lại yêu cầu thanh toán Pending.
    /// <summary>
    /// Tạo hoặc lấy lại yêu cầu thanh toán đang chờ xử lý
    /// </summary>
    [HttpPost]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentDTO>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentDTO dto)
    {
        var response = await _paymentService.CreateAsync(
            dto,
            ActorId());

        return StatusCode(response.StatusCode, response);
    }

    // POST /api/payments/{id}/sync
    // Đồng bộ trạng thái thực tế từ payOS.
    /// <summary>
    /// Đồng bộ trạng thái thanh toán từ payOS
    /// </summary>
    [HttpPost("{id:int:min(1)}/sync")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentDTO>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(int id)
    {
        var response = await _paymentService.SyncAsync(
            id,
            ActorId());

        return StatusCode(response.StatusCode, response);
    }

    // POST /api/payments/{id}/cancel
    /// <summary>
    /// Hủy yêu cầu thanh toán payOS
    /// </summary>
    [HttpPost("{id:int:min(1)}/cancel")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentDTO>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelPaymentDTO dto)
    {
        var response = await _paymentService.CancelAsync(
            id,
            dto.Reason,
            ActorId());

        return StatusCode(response.StatusCode, response);
    }

    // POST /api/payments/payos/webhook
    // Service xác minh chữ ký trước khi xử lý dữ liệu payOS.
    /// <summary>
    /// Nhận webhook payOS và xác minh chữ ký giao dịch
    /// </summary>
    [AllowAnonymous]
    [HttpPost("payos/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PayOSWebhook(
        [FromBody] Webhook dto)
    {
        try
        {
            await _paymentService.PayOSWebhookAsync(dto);

            return Ok(new
            {
                success = true
            });
        }
        catch (WebhookException)
        {
            return BadRequest(
                ApiResponse<object>.BadRequest(
                    "Dữ liệu hoặc chữ ký webhook không hợp lệ."));
        }
    }

    /// <summary>
    /// Xuất biên lai PDF của giao dịch đã hoàn tất
    /// </summary>
    /// <remarks>Hội viên chỉ được tải biên lai của mình. Lễ tân và quản lý được tải biên lai của hội viên.</remarks>
    [HttpGet("{id:int:min(1)}/export-pdf")]
    [ProducesResponseType(
        typeof(FileContentResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportInvoicePdf(
        int id,
        CancellationToken ct)
    {
        int actorId = ActorId();

        // 1. Tìm giao dịch và kiểm tra chủ sở hữu.
        var query = _context.Payments
            .AsNoTracking()
            .Where(p => p.Id == id);

        if (User.IsInRole("Member"))
        {
            query = query.Where(p => p.Member.UserId == actorId);
        }

        var payment = await query
            .Include(p => p.Member)
                .ThenInclude(m => m.User)
            .Include(p => p.Subscription)
                .ThenInclude(s => s!.Package)
            .SingleOrDefaultAsync(ct);

        if (payment == null)
        {
            return NotFound(new
            {
                message =
                    "Không tìm thấy giao dịch bạn được phép truy cập."
            });
        }

        // 2. Chỉ xuất khi giao dịch đã hoàn tất.
        if (payment.Status != "Completed")
        {
            return BadRequest(new
            {
                message =
                    "Chỉ xuất biên lai cho giao dịch đã hoàn tất."
            });
        }

        // 3. Ưu tiên thông tin được lưu lúc thanh toán.
        string invoiceCode =
            payment.TransactionReference ?? $"INV-{payment.Id}";

        string memberName =
            payment.MemberNameSnapshot ??
            payment.Member.User.FullName;

        string packageName =
            payment.PackageNameSnapshot ??
            payment.Subscription?.Package?.PackageName ??
            "Gói tập";

        string paymentMethod = payment.PaymentMethod switch
        {
            "Cash" => "Tiền mặt",
            "BankTransfer" => "Chuyển khoản",
            "CreditCard" => "Thẻ tín dụng",
            "PayOS" => "payOS",
            _ => payment.PaymentMethod
        };

        var culture = CultureInfo.GetCultureInfo("vi-VN");

        string totalAmount =
            payment.AmountReceived.ToString("N0", culture) + " VNĐ";

        // Database lưu UTC; biên lai hiển thị giờ Việt Nam.
        var paymentDateUtc = DateTime.SpecifyKind(
            payment.PaymentDate,
            DateTimeKind.Utc);

        var paymentDateVietnam = new DateTimeOffset(paymentDateUtc)
            .ToOffset(TimeSpan.FromHours(7));

        // 4. Tạo tài liệu PDF.
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A5);
                page.Margin(28);
                page.PageColor("#FFFFFF");

                page.DefaultTextStyle(style =>
                    style.FontSize(10).FontColor("#243447"));

                // Tên trung tâm và trạng thái.
                page.Header()
                    .BorderBottom(3)
                    .BorderColor("#13A89E")
                    .PaddingBottom(16)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(brand =>
                        {
                            brand.Item()
                                .Text("SPORTS CENTER")
                                .FontSize(20).Bold()
                                .FontColor("#102B3F");

                            brand.Item().PaddingTop(4)
                                .Text("TẬP LUYỆN HÔM NAY. KHỎE HƠN MỖI NGÀY.")
                                .FontSize(7).FontColor("#64748B");
                        });

                        row.ConstantItem(82)
                            .AlignRight().AlignMiddle()
                            .Background("#E5F6EF").Padding(7)
                            .Text("ĐÃ THANH TOÁN")
                            .FontSize(8).Bold().FontColor("#18734C");
                    });

                page.Content().PaddingTop(22).Column(content =>
                {
                    content.Spacing(16);

                    // Tiêu đề và mã biên lai.
                    content.Item().Column(title =>
                    {
                        title.Item()
                            .Text("BIÊN LAI THANH TOÁN")
                            .FontSize(21).Bold().FontColor("#102B3F");

                        title.Item().PaddingTop(5)
                            .Text(invoiceCode)
                            .FontSize(9).FontColor("#64748B");
                    });

                    // Thông tin giao dịch.
                    content.Item()
                        .Background("#F3F6F9")
                        .Padding(13)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(info =>
                            {
                                info.Item().Text("NGÀY THANH TOÁN")
                                    .FontSize(7).FontColor("#64748B");

                                info.Item().PaddingTop(5)
                                    .Text($"{paymentDateVietnam:dd/MM/yyyy HH:mm} (UTC+7)")
                                    .SemiBold();
                            });

                            row.RelativeItem().Column(info =>
                            {
                                info.Item().Text("PHƯƠNG THỨC")
                                    .FontSize(7).FontColor("#64748B");

                                info.Item().PaddingTop(5)
                                    .Text(paymentMethod).SemiBold();
                            });
                        });

                    // Thông tin hội viên.
                    content.Item().Column(customer =>
                    {
                        customer.Item().Text("THÔNG TIN HỘI VIÊN")
                            .FontSize(8).SemiBold().FontColor("#64748B");

                        customer.Item().PaddingTop(6)
                            .Text(memberName).FontSize(13).SemiBold();
                    });

                    // Bảng dịch vụ.
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell()
                                .Background("#102B3F").Padding(10)
                                .Text("DỊCH VỤ / GÓI TẬP")
                                .FontSize(8).SemiBold().FontColor("#FFFFFF");

                            header.Cell()
                                .Background("#102B3F").Padding(10).AlignRight()
                                .Text("THÀNH TIỀN")
                                .FontSize(8).SemiBold().FontColor("#FFFFFF");
                        });

                        table.Cell()
                            .BorderBottom(1).BorderColor("#E2E8F0")
                            .Padding(10).Text(packageName);

                        table.Cell()
                            .BorderBottom(1).BorderColor("#E2E8F0")
                            .Padding(10).AlignRight()
                            .Text(totalAmount).SemiBold();
                    });

                    // Tổng tiền.
                    content.Item()
                        .Background("#EAF7F5")
                        .Padding(15)
                        .Column(total =>
                        {
                            total.Item()
                                .Text("TỔNG TIỀN ĐÃ THANH TOÁN")
                                .FontSize(8).SemiBold().FontColor("#28736D");

                            total.Item().PaddingTop(5)
                                .Text(totalAmount)
                                .FontSize(25).Bold().FontColor("#102B3F");
                        });

                    content.Item()
                        .Text("Vui lòng lưu biên lai để đối chiếu giao dịch khi cần hỗ trợ.")
                        .FontSize(8).FontColor("#64748B");
                });

                // Chân trang.
                page.Footer()
                    .BorderTop(1).BorderColor("#E2E8F0")
                    .PaddingTop(12)
                    .Column(footer =>
                    {
                        footer.Item()
                            .Text("Cảm ơn bạn đã đồng hành cùng Sports Center!")
                            .FontSize(9).SemiBold().FontColor("#102B3F");

                        footer.Item().PaddingTop(4)
                            .Text("Biên lai được tạo từ giao dịch đã hoàn tất.")
                            .FontSize(7).FontColor("#64748B");
                    });
            });
        });

        // 5. Trả file để tải xuống hoặc in.
        byte[] pdfBytes = document.GeneratePdf();

        return File(
            pdfBytes,
            "application/pdf",
            $"BienLai_{payment.Id}.pdf");
    }

    // Lấy ID tài khoản từ JWT.
    private int ActorId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out int actorId) || actorId <= 0)
        {
            throw new BusinessException(
                StatusCodes.Status401Unauthorized,
                "Thông tin đăng nhập không hợp lệ.");
        }

        return actorId;
    }
}