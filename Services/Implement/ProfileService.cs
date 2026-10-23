using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Profile;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;

    public ProfileService(AppDbContext db)
    {
        _db = db;
    }

    private async Task<User> FindUserAsync(int userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(
            u => u.Id == userId && u.IsActive);

        if (user == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy tài khoản đang hoạt động.");
        }

        return user;
    }

    public async Task<ProfileDTO> GetAsync(int userId)
    {
        var user = await FindUserAsync(userId);

        return new ProfileDTO(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            RoleName: user.Role,
            Status: "Active",
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }

    public async Task<ProfileDTO> UpdateAsync(
        int userId,
        UpdateProfileDTO dto)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var user = await FindUserAsync(userId);

        var email = dto.Email?.Trim().ToLowerInvariant();

        var changesEmail =
            email != null &&
            !string.Equals(
                email,
                user.Email,
                StringComparison.OrdinalIgnoreCase);

        // Xác nhận mật khẩu hiện tại khi đổi email hoặc mật khẩu.
        if (changesEmail || dto.Password != null)
        {
            if (string.IsNullOrEmpty(dto.CurrentPassword))
            {
                throw new BusinessException(
                    400,
                    "Vui lòng nhập mật khẩu hiện tại.");
            }

            var validPassword = PasswordHelper.VerifyPassword(
                dto.CurrentPassword,
                user.PasswordHash);

            if (!validPassword)
            {
                throw new BusinessException(
                    400,
                    "Mật khẩu hiện tại không chính xác.");
            }
        }

        if (dto.FullName != null)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
            {
                throw new BusinessException(
                    400,
                    "Họ tên không được để trống.");
            }

            user.FullName = dto.FullName.Trim();
        }

        if (changesEmail)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new BusinessException(
                    400,
                    "Email không được để trống.");
            }

            var emailExists = await _db.Users.AnyAsync(
                u => u.Id != userId &&
                     u.Email.ToLower() == email);

            if (emailExists)
            {
                throw new BusinessException(
                    409,
                    "Email đã được sử dụng.");
            }

            user.Email = email;
        }

        if (dto.PhoneNumber != null)
        {
            var phone = dto.PhoneNumber.Trim();

            if (string.IsNullOrWhiteSpace(phone))
            {
                throw new BusinessException(
                    400,
                    "Số điện thoại không được để trống.");
            }

            var phoneExists = await _db.Users.AnyAsync(
                u => u.Id != userId &&
                     u.PhoneNumber == phone);

            if (phoneExists)
            {
                throw new BusinessException(
                    409,
                    "Số điện thoại đã được sử dụng.");
            }

            user.PhoneNumber = phone;
        }

        if (dto.Password != null)
        {
            user.PasswordHash =
                PasswordHelper.HashPassword(dto.Password);
        }

        // Thu hồi refresh token khi thay đổi thông tin đăng nhập.
        if (changesEmail || dto.Password != null)
        {
            await _db.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        t => t.IsRevoked,
                        true));
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetAsync(userId);
    }
}
