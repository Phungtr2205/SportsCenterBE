using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Profile;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;

    private sealed class ChangeEmailPending
    {
        public required string NewEmail { get; init; }
        public required string Otp { get; init; }
        public int Attempts { get; set; }
    }

    public ProfileService(AppDbContext db, IEmailService emailService, IMemoryCache cache)
    {
        _db = db;
        _emailService = emailService;
        _cache = cache;
    }

    private async Task<User> FindUserAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null)
            throw new BusinessException(404, "Không tìm thấy tài khoản đang hoạt động.");
        return user;
    }

    public async Task<ProfileDTO> GetAsync(int userId)
    {
        var user = await FindUserAsync(userId);
        var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == userId);

        return new ProfileDTO(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            RoleName: user.Role,
            Status: "Active",
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt,
            DateOfBirth: member?.DateOfBirth,
            Gender: member?.Gender,
            Address: member?.Address,
            FitnessGoal: member?.FitnessGoal,
            EmergencyContact: member?.EmergencyContact
        );
    }

    public async Task<ProfileDTO> UpdateAsync(int userId, UpdateProfileDTO dto)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await FindUserAsync(userId);

        // Chỉ bắt mật khẩu hiện tại khi thực sự muốn đổi mật khẩu mới
        if (dto.Password != null)
        {
            if (string.IsNullOrEmpty(dto.CurrentPassword))
                throw new BusinessException(400, "Vui lòng nhập mật khẩu hiện tại.");

            var validPassword = PasswordHelper.VerifyPassword(dto.CurrentPassword, user.PasswordHash);
            if (!validPassword)
                throw new BusinessException(400, "Mật khẩu hiện tại không chính xác.");

            user.PasswordHash = PasswordHelper.HashPassword(dto.Password);

            // Thu hồi refresh token khi đổi mật khẩu
            await _db.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
        }

        if (dto.FullName != null)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new BusinessException(400, "Họ tên không được để trống.");
            user.FullName = dto.FullName.Trim();
        }

        if (dto.PhoneNumber != null)
        {
            var phone = dto.PhoneNumber.Trim();
            if (string.IsNullOrWhiteSpace(phone))
                throw new BusinessException(400, "Số điện thoại không được để trống.");

            var phoneExists = await _db.Users.AnyAsync(u => u.Id != userId && u.PhoneNumber == phone);
            if (phoneExists)
                throw new BusinessException(409, "Số điện thoại đã được sử dụng.");

            user.PhoneNumber = phone;
        }

        // Cập nhật thông tin chi tiết vào bảng Members
        var member = await _db.Members.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member != null)
        {
            if (dto.DateOfBirth.HasValue)
            {
                var dob = dto.DateOfBirth.Value;
                if (dob.Date > DateTime.UtcNow.Date)
                    throw new BusinessException(400, "Ngày sinh không được ở tương lai.");

                member.DateOfBirth = DateTime.SpecifyKind(dob.Date, DateTimeKind.Utc);
            }

            if (dto.Gender != null) member.Gender = dto.Gender.Trim();
            if (dto.Address != null) member.Address = dto.Address.Trim();
            if (dto.FitnessGoal != null) member.FitnessGoal = dto.FitnessGoal.Trim();
            if (dto.EmergencyContact != null) member.EmergencyContact = dto.EmergencyContact.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetAsync(userId);
    }

    // 1. Gửi mã OTP xác thực tới hộp thư email mới
    public async Task SendChangeEmailOtpAsync(int userId, RequestChangeEmailOtpDTO dto)
    {
        var user = await FindUserAsync(userId);
        var newEmail = dto.NewEmail.Trim().ToLowerInvariant();

        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(400, "Email mới phải khác với email hiện tại.");

        var exists = await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == newEmail);
        if (exists)
            throw new BusinessException(409, "Email này đã được sử dụng bởi một tài khoản khác.");

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var cacheKey = $"sportscenter:change-email:{userId}";

        _cache.Set(cacheKey, new ChangeEmailPending
        {
            NewEmail = newEmail,
            Otp = otp,
            Attempts = 0
        }, TimeSpan.FromMinutes(5));

        await _emailService.SendEmailAsync(
            newEmail,
            "Sports Center | Xác nhận thay đổi email tài khoản",
            $"Mã xác nhận đổi email của bạn là {otp}. Mã có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.",
            OtpEmailTemplate.Create(otp, resetPassword: false)
        );
    }

    // 2. Xác thực mã OTP và cập nhật email chính thức
    public async Task<ProfileDTO> VerifyChangeEmailOtpAsync(int userId, VerifyChangeEmailOtpDTO dto)
    {
        var user = await FindUserAsync(userId);
        var newEmail = dto.NewEmail.Trim().ToLowerInvariant();
        var cacheKey = $"sportscenter:change-email:{userId}";

        if (!_cache.TryGetValue(cacheKey, out ChangeEmailPending? pending) || pending == null)
            throw new BusinessException(400, "Mã xác nhận đã hết hạn hoặc không tồn tại. Vui lòng gửi lại mã mới.");

        if (!string.Equals(pending.NewEmail, newEmail, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(400, "Email xác nhận không khớp với email đã yêu cầu.");

        if (pending.Attempts >= 5)
        {
            _cache.Remove(cacheKey);
            throw new BusinessException(400, "Bạn đã nhập sai mã xác nhận quá 5 lần. Vui lòng yêu cầu mã mới.");
        }

        if (pending.Otp != dto.Otp.Trim())
        {
            pending.Attempts++;
            throw new BusinessException(400, "Mã xác nhận OTP không chính xác.");
        }

        // Cập nhật email mới vào cơ sở dữ liệu
        user.Email = newEmail;
        user.UpdatedAt = DateTime.UtcNow;

        // Thu hồi refresh token cũ
        // Thu hồi refresh token cũ
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        await _db.SaveChangesAsync();
        _cache.Remove(cacheKey);

        return await GetAsync(userId);
    }
}