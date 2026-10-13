using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.CheckIn;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class GymCheckInService : IGymCheckInService
{
    private readonly AppDbContext _context;

    public GymCheckInService(AppDbContext context)
    {
        _context = context;
    }

    // 1. Hội viên lấy thông tin thẻ và chuỗi QR của mình
    public async Task<MemberQrResponse> GetMyQrCodeAsync(int userId)
    {
        var member = await _context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin hội viên của tài khoản này.");

        var now = DateTime.UtcNow;
        var activeSub = await _context.MemberSubscriptions
            .Include(s => s.Package)
            .Where(s => s.MemberId == member.Id && s.Status == "Active")
            .Where(s => s.StartDate <= now && s.EndDate >= now)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        string memberCode = $"SC-MEM-{member.Id:D6}";

        return new MemberQrResponse
        {
            MemberId = member.Id,
            MemberCode = memberCode,
            QrData = memberCode,
            FullName = member.User.FullName,
            PhoneNumber = member.User.PhoneNumber ?? string.Empty,
            PackageName = activeSub?.Package.PackageName,
            PackageStatus = activeSub != null ? "Active" : "NoneOrExpired",
            ExpireDate = activeSub?.EndDate
        };
    }

    // 2. Lễ tân quét mã QR điểm danh
    public async Task<GymCheckInResponse> ScanQrCheckInAsync(int staffUserId, ScanQrRequest request)
    {
        var rawCode = request.QrCode.Trim();
        int? memberId = null;

        // Bóc tách mã: hỗ trợ dạng "SC-MEM-000001", "SC-1", hoặc số ID trực tiếp "1"
        if (rawCode.StartsWith("SC-MEM-", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(rawCode.Substring(7), out int parsedId)) memberId = parsedId;
        }
        else if (rawCode.StartsWith("SC-", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(rawCode.Substring(3), out int parsedId)) memberId = parsedId;
        }
        else if (int.TryParse(rawCode, out int directId))
        {
            memberId = directId;
        }

        // Tự động phân luồng: nếu giải mã được ID thì tìm theo ID, ngược lại coi chuỗi là SĐT
        var checkInReq = new CheckInRequest
        {
            MemberId = memberId,
            PhoneNumber = memberId.HasValue ? null : rawCode,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? $"Quét mã QR: {rawCode}" : request.Notes
        };

        return await CheckInMemberAsync(staffUserId, checkInReq);
    }

    public async Task<GymCheckInResponse> CheckInMemberAsync(int staffUserId, CheckInRequest request)
    {
        // 1. Tìm Member theo MemberId HOẶC Số điện thoại
        Member? member = null;
        if (request.MemberId.HasValue && request.MemberId.Value > 0)
        {
            member = await _context.Members
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var user = await _context.Users
                .Include(u => u.Member)
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

            member = user?.Member;
            if (member != null && member.User == null)
            {
                member.User = user!;
            }
        }

        if (member == null)
            throw new KeyNotFoundException("Không tìm thấy hội viên với mã hoặc số điện thoại cung cấp.");

        // 2. Kiểm tra gói tập còn hạn không
        var now = DateTime.UtcNow;
        var activeSub = await _context.MemberSubscriptions
            .Include(s => s.Package)
            .Where(s => s.MemberId == member.Id && s.Status == "Active")
            .Where(s => s.StartDate <= now && s.EndDate >= now)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        if (activeSub == null)
            throw new InvalidOperationException("Hội viên không có gói tập nào đang hoạt động hoặc gói tập đã hết hạn!");

        // 3. Tránh check-in trùng lặp nếu chưa check-out trong ngày
        var openCheckIn = await _context.GymCheckIns
            .Where(g => g.MemberId == member.Id && g.CheckOutTime == null)
            .Where(g => g.CheckInTime >= now.Date)
            .FirstOrDefaultAsync();

        if (openCheckIn != null)
            throw new InvalidOperationException("Hội viên này đã check-in trước đó và chưa check-out!");

        // 4. Lưu lượt Check-in
        var checkIn = new GymCheckIn
        {
            MemberId = member.Id,
            SubscriptionId = activeSub.Id,
            CheckInTime = now,
            CheckedInByUserId = staffUserId,
            Notes = request.Notes
        };

        _context.GymCheckIns.Add(checkIn);
        await _context.SaveChangesAsync();

        var staff = await _context.Users.FindAsync(staffUserId);
        return MapToResponse(checkIn, member, activeSub.Package.PackageName, staff?.FullName);
    }

    public async Task<GymCheckInResponse> CheckOutMemberAsync(int checkInId)
    {
        var checkIn = await _context.GymCheckIns
            .Include(g => g.Member).ThenInclude(m => m.User)
            .Include(g => g.Subscription).ThenInclude(s => s!.Package)
            .Include(g => g.CheckedByUser)
            .FirstOrDefaultAsync(g => g.Id == checkInId)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt check-in.");

        if (checkIn.CheckOutTime != null)
            throw new InvalidOperationException("Lượt check-in này đã được check-out trước đó.");

        checkIn.CheckOutTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToResponse(checkIn, checkIn.Member, checkIn.Subscription?.Package.PackageName ?? "Gói tập", checkIn.CheckedByUser?.FullName);
    }

    public async Task<IEnumerable<GymCheckInResponse>> GetTodayCheckInsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var checkIns = await _context.GymCheckIns
            .Include(g => g.Member).ThenInclude(m => m.User)
            .Include(g => g.Subscription).ThenInclude(s => s!.Package)
            .Include(g => g.CheckedByUser)
            .Where(g => g.CheckInTime >= today)
            .OrderByDescending(g => g.CheckInTime)
            .ToListAsync();

        return checkIns.Select(g => MapToResponse(g, g.Member, g.Subscription?.Package.PackageName ?? "Gói tập", g.CheckedByUser?.FullName));
    }

    public async Task<IEnumerable<GymCheckInResponse>> GetMyHistoryAsync(int userId)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member == null) return new List<GymCheckInResponse>();

        var checkIns = await _context.GymCheckIns
            .Include(g => g.Member).ThenInclude(m => m.User)
            .Include(g => g.Subscription).ThenInclude(s => s!.Package)
            .Include(g => g.CheckedByUser)
            .Where(g => g.MemberId == member.Id)
            .OrderByDescending(g => g.CheckInTime)
            .ToListAsync();

        return checkIns.Select(g => MapToResponse(g, g.Member, g.Subscription?.Package.PackageName ?? "Gói tập", g.CheckedByUser?.FullName));
    }

    private static GymCheckInResponse MapToResponse(GymCheckIn g, Member member, string packageName, string? staffName)
    {
        return new GymCheckInResponse
        {
            Id = g.Id,
            MemberId = member.Id,
            MemberCode = $"SC-MEM-{member.Id:D6}",
            MemberName = member.User?.FullName ?? "Hội viên",
            PhoneNumber = member.User?.PhoneNumber ?? string.Empty,
            PackageName = packageName,
            CheckInTime = g.CheckInTime,
            CheckOutTime = g.CheckOutTime,
            CheckedByStaffName = staffName,
            Notes = g.Notes
        };
    }
}