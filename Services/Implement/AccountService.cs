using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Accounts;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class AccountService : IAccountService
{
    private readonly AppDbContext _context;

    public AccountService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách hội viên.
    public async Task<IReadOnlyList<MemberResponseDTO>> GetMembersAsync()
    {
        var members = await _context.Members
            .AsNoTracking()
            .Include(member => member.User)
            .OrderByDescending(member => member.Id)
            .ToListAsync();

        return members
            .Select(MemberResponseDTO.FromModel)
            .ToList();
    }

    // Lấy một hội viên theo ID.
    public async Task<MemberResponseDTO> GetMemberAsync(int memberId)
    {
        var member = await _context.Members
            .AsNoTracking()
            .Include(member => member.User)
            .SingleOrDefaultAsync(member => member.Id == memberId);

        if (member == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy hội viên.");
        }

        return MemberResponseDTO.FromModel(member);
    }

    // Lấy hồ sơ hội viên của người đang đăng nhập.
    public async Task<MemberResponseDTO> GetMyMemberAsync(int userId)
    {
        var member = await _context.Members
            .AsNoTracking()
            .Include(member => member.User)
            .SingleOrDefaultAsync(member => member.UserId == userId);

        if (member == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy hồ sơ hội viên.");
        }

        return MemberResponseDTO.FromModel(member);
    }

    // Vô hiệu hóa hội viên và ghi lại người thực hiện.
    public async Task DeactivateMemberAsync(
        int memberId,
        int callerUserId)
    {
        await EnsureManagerAsync(callerUserId);

        var member = await _context.Members
            .Include(member => member.User)
            .SingleOrDefaultAsync(member => member.Id == memberId);

        if (member == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy hội viên.");
        }

        if (!member.User.IsActive)
        {
            return;
        }

        member.User.IsActive = false;
        member.User.UpdatedAt = DateTime.UtcNow;

        AddAccountAudit(
            callerUserId,
            "Delete",
            nameof(Member),
            member.Id,
            new
            {
                member.UserId,
                SoftDelete = true,
                Before = new { IsActive = true },
                After = new { IsActive = false }
            });

        await _context.SaveChangesAsync();
    }

    // Lấy danh sách nhân viên.
    public async Task<IReadOnlyList<StaffResponseDTO>> GetStaffAsync()
    {
        var staff = await StaffQuery()
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .ToListAsync();

        return staff
            .Select(StaffResponseDTO.FromModel)
            .ToList();
    }

    // Lấy một nhân viên theo ID.
    public async Task<StaffResponseDTO> GetStaffAsync(int userId)
    {
        var user = await StaffQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId);

        if (user == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy tài khoản nhân viên.");
        }

        return StaffResponseDTO.FromModel(user);
    }

    // Tạo nhân viên và ghi audit trong cùng một transaction.
    public async Task<StaffResponseDTO> CreateStaffAsync(
        CreateStaffRequestDTO request,
        int callerUserId)
    {
        await EnsureManagerAsync(callerUserId);

        ValidateStaffRole(request.Role);

        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Users.AnyAsync(
            user => user.Email.ToLower() == email);

        if (emailExists)
        {
            throw new BusinessException(
                409,
                "Email đã được đăng ký.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.Phone?.Trim(),
            PasswordHash = PasswordHelper.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (request.Role == UserRoles.Coach)
        {
            user.Coach = new Coach();
        }

        _context.Users.Add(user);

        // Lưu trước để lấy ID tài khoản.
        await _context.SaveChangesAsync();

        AddAccountAudit(
            callerUserId,
            "Create",
            nameof(User),
            user.Id,
            new
            {
                user.Role,
                user.IsActive
            });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return StaffResponseDTO.FromModel(user);
    }

    // Đổi quyền nhân viên.
    public async Task<StaffResponseDTO> UpdateStaffRoleAsync(
        int userId,
        UpdateStaffRoleRequestDTO request,
        int callerUserId)
    {
        await EnsureManagerAsync(callerUserId);

        ValidateStaffRole(request.Role);

        var user = await StaffQuery()
            .SingleOrDefaultAsync(user => user.Id == userId);

        if (user == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy tài khoản nhân viên.");
        }

        if (user.Role == request.Role)
        {
            return StaffResponseDTO.FromModel(user);
        }

        if (userId == callerUserId)
        {
            throw new BusinessException(
                409,
                "Bạn không thể tự đổi quyền của mình.");
        }

        var oldRole = user.Role;

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;

        if (request.Role == UserRoles.Coach && user.Coach == null)
        {
            user.Coach = new Coach();
        }

        // Giữ hồ sơ Coach cũ khi đổi sang quyền khác
        // để lịch sử lớp học vẫn giữ được liên kết.
        AddAccountAudit(
            callerUserId,
            "Update",
            nameof(User),
            user.Id,
            new
            {
                Before = new { Role = oldRole },
                After = new { Role = user.Role }
            });

        await _context.SaveChangesAsync();

        return StaffResponseDTO.FromModel(user);
    }

    // Vô hiệu hóa nhân viên.
    public async Task DeactivateStaffAsync(
        int userId,
        int callerUserId)
    {
        await EnsureManagerAsync(callerUserId);

        if (userId == callerUserId)
        {
            throw new BusinessException(
                409,
                "Bạn không thể vô hiệu hóa tài khoản của mình.");
        }

        var user = await StaffQuery()
            .SingleOrDefaultAsync(user => user.Id == userId);

        if (user == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy tài khoản nhân viên.");
        }

        if (!user.IsActive)
        {
            return;
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        AddAccountAudit(
            callerUserId,
            "Delete",
            nameof(User),
            user.Id,
            new
            {
                SoftDelete = true,
                Before = new { IsActive = true },
                After = new { IsActive = false }
            });

        await _context.SaveChangesAsync();
    }

    // Truy vấn các tài khoản nhân viên.
    private IQueryable<User> StaffQuery()
    {
        return _context.Users
            .Include(user => user.Coach)
            .Where(user =>
                user.Member == null &&
                (
                    user.Role == UserRoles.Manager ||
                    user.Role == UserRoles.Receptionist ||
                    user.Role == UserRoles.Coach
                ));
    }

    // Kiểm tra quyền được phép gán cho nhân viên.
    private static void ValidateStaffRole(string role)
    {
        if (!UserRoles.IsStaff(role))
        {
            throw new BusinessException(
                400,
                "Quyền nhân viên phải là Manager, Receptionist hoặc Coach.");
        }
    }

    // Kiểm tra người thực hiện là quản lý đang hoạt động.
    private async Task EnsureManagerAsync(int actorId)
    {
        var isManager = await _context.Users.AnyAsync(user =>
            user.Id == actorId &&
            user.IsActive &&
            user.Role == UserRoles.Manager);

        if (!isManager)
        {
            throw new BusinessException(
                403,
                "Chỉ quản lý đang hoạt động được thực hiện thao tác này.");
        }
    }

    // Thêm audit vào DbContext.
    // Hàm gọi bên trên sẽ SaveChanges cùng thay đổi tài khoản.
    private void AddAccountAudit(
        int actorId,
        string action,
        string entityName,
        int entityId,
        object details)
    {
        var audit = new AuditLog
        {
            UserId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = JsonSerializer.Serialize(details),
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
    }
}