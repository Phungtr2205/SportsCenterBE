using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Accounts;
using SportsCenterAPI.DTOs.Auth;
using SportsCenterAPI.DTOs.Login;
using SportsCenterAPI.DTOs.Register;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;
using System.Security.Cryptography;
using System.Text;

namespace SportsCenterAPI.Services.Implement;

public class AuthService(
    AppDbContext context,
    IConfiguration configuration,
    RedisOtpStore otpStore,
    IEmailService emailService) : IAuthService
{
    private sealed class PendingRegistration
    {
        public required RegisterRequestDTO Request { get; init; }
        public required string PasswordHash { get; init; }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequestDTO request)
    {
        var email = NormalizeEmail(request.Email);

        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

        if (user is null ||
            !PasswordHelper.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequestDTO request)
    {
        var user = await CreateMemberAccountAsync(request);
        return await CreateAuthResponseAsync(user);
    }

    // Lễ tân tạo tài khoản cho member, nhưng không đăng nhập thay member.
    public async Task<MemberResponseDTO> RegisterAtDeskAsync(
        RegisterRequestDTO request)
    {
        var user = await CreateMemberAccountAsync(request);
        return MemberResponseDTO.FromModel(user.Member!);
    }

    public Task<bool> IsEmailExistingAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        return context.Users.AnyAsync(
            u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task SendRegisterOtpAsync(RegisterRequestDTO request)
    {
        var email = NormalizeEmail(request.Email);

        if (await IsEmailExistingAsync(email))
        {
            throw new BusinessException(
                StatusCodes.Status409Conflict,
                "Email is already registered.");
        }

        if (request.DateOfBirth?.Date > DateTime.UtcNow.Date)
        {
            throw new BusinessException(
                StatusCodes.Status400BadRequest,
                "Date of birth cannot be in the future.");
        }

        var otp = GenerateOtp();

        // Chỉ lưu mật khẩu đã băm; dữ liệu đăng ký không giữ Password gốc.
        var pending = new PendingRegistration
        {
            Request = new RegisterRequestDTO
            {
                Email = email,
                FullName = request.FullName,
                Phone = request.Phone,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                Address = request.Address,
                FitnessGoal = request.FitnessGoal
            },
            PasswordHash = PasswordHelper.HashPassword(request.Password)
        };

        await otpStore.SaveAsync(
            RegistrationKey(email), HashOtp(otp), pending);

        await emailService.SendEmailAsync(
    email,
    "Sports Center | Xác nhận đăng ký",
    $"Mã xác nhận đăng ký của bạn là {otp}. " +
    "Mã có hiệu lực trong 5 phút. Không chia sẻ mã với bất kỳ ai.",
    OtpEmailTemplate.Create(otp, resetPassword: false));
    }

    public async Task<AuthResponse> VerifyRegisterOtpAsync(
        VerifyRegisterOtpDTO request)
    {
        var email = NormalizeEmail(request.Email);
        var key = RegistrationKey(email);

        // Redis kiểm tra số lần sai và xóa OTP đúng trong cùng một thao tác.
        var pending = await otpStore.ConsumeAsync<PendingRegistration>(
            key, HashOtp(request.Otp))
            ?? throw InvalidOtpException();

        // Nếu database lỗi sau khi mã đã được dùng, yêu cầu gửi OTP mới.
        var user = await CreateMemberAccountAsync(
            pending.Request, pending.PasswordHash);

        return await CreateAuthResponseAsync(user);
    }

    public async Task RequestResetPasswordOtpAsync(RequestOtpDTO request)
    {
        var email = NormalizeEmail(request.Email);

        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

        // Không tiết lộ email này có tài khoản hay không.
        if (user is null)
            return;

        var otp = GenerateOtp();

        await otpStore.SaveAsync(
            ResetPasswordKey(email), HashOtp(otp), email);

        await emailService.SendEmailAsync(
           email,
           "Sports Center | Đặt lại mật khẩu",
           $"Mã đặt lại mật khẩu của bạn là {otp}. " +
           "Mã có hiệu lực trong 5 phút. " +
           "Nếu không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.",
           OtpEmailTemplate.Create(otp, resetPassword: true));
    }

    public async Task VerifyResetPasswordOtpAsync(
        VerifyResetPasswordOtpDTO request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new BusinessException(
                StatusCodes.Status400BadRequest,
                "Passwords do not match.");
        }

        var email = NormalizeEmail(request.Email);
        var key = ResetPasswordKey(email);

        var storedEmail = await otpStore.ConsumeAsync<string>(
            key, HashOtp(request.Otp));
        if (storedEmail != email)
            throw InvalidOtpException();

        var user = await context.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == email && u.IsActive);

        if (user is null)
            throw InvalidOtpException();

        user.PasswordHash =
            PasswordHelper.HashPassword(request.NewPassword);

        // Save the password and refresh-token revocations in the same transaction.
        var refreshTokens = await context.RefreshTokens
            .Where(token => token.UserId == user.Id && !token.IsRevoked)
            .ToListAsync();
        foreach (var token in refreshTokens)
            token.IsRevoked = true;

        await context.SaveChangesAsync();
    }

    private async Task<User> CreateMemberAccountAsync(
        RegisterRequestDTO request,
        string? passwordHash = null)
    {
        var email = NormalizeEmail(request.Email);

        if (await IsEmailExistingAsync(email))
        {
            throw new BusinessException(
                StatusCodes.Status409Conflict,
                "Email is already registered.");
        }

        if (request.DateOfBirth?.Date > DateTime.UtcNow.Date)
        {
            throw new BusinessException(
                StatusCodes.Status400BadRequest,
                "Date of birth cannot be in the future.");
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash ?? PasswordHelper.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.Phone?.Trim(),
            Role = UserRoles.Member,
            IsActive = true,
            CreatedAt = now,
            Member = new Member
            {
                Address = request.Address?.Trim(),
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender?.Trim(),
                FitnessGoal = request.FitnessGoal?.Trim(),
                JoinDate = now
            }
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<AuthResponse> RenewToken(RefreshTokenDTO tokenDTO)
    {
        ValidateRefreshTokenRequest(tokenDTO);
        var tokenHash = HashRefreshToken(tokenDTO.RefreshTokenKey);

        // Tracked so RowVersion prevents two requests from rotating the same token.
        var storedToken = await context.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash);

        if (storedToken is null || storedToken.IsRevoked ||
            storedToken.ExpiresAt <= DateTime.UtcNow || !storedToken.User.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var refreshTokenKey = GenerateRefreshToken();
        var response = CreateAuthResponse(storedToken.User);
        response.RefreshTokenKey = refreshTokenKey;
        response.RefreshTokenExpiresAt = DateTime.SpecifyKind(storedToken.ExpiresAt, DateTimeKind.Utc);

        // Rotate the secret without extending the original session lifetime.
        storedToken.TokenHash = HashRefreshToken(refreshTokenKey);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedAccessException("Refresh token has already been used or revoked.");
        }

        return response;
    }

    public async Task Logout(RefreshTokenDTO tokenDTO)
    {
        ValidateRefreshTokenRequest(tokenDTO);
        var tokenHash = HashRefreshToken(tokenDTO.RefreshTokenKey);

        // Idempotent, atomic revocation. The client must send its latest refresh token.
        await context.RefreshTokens
            .Where(token => token.TokenHash == tokenHash && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true));
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user)
    {
        var expireDays = configuration.GetValue<int>("Jwt:RefreshTokenExpireDays", 7);
        if (expireDays <= 0)
            throw new InvalidOperationException("Jwt:RefreshTokenExpireDays must be positive.");

        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(expireDays);
        var refreshTokenKey = GenerateRefreshToken();
        var response = CreateAuthResponse(user);
        response.RefreshTokenKey = refreshTokenKey;
        response.RefreshTokenExpiresAt = expiresAt;

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshTokenKey),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        await context.SaveChangesAsync();
        return response;
    }

    private static void ValidateRefreshTokenRequest(RefreshTokenDTO tokenDTO)
    {
        if (tokenDTO is null || string.IsNullOrWhiteSpace(tokenDTO.RefreshTokenKey) ||
            tokenDTO.RefreshTokenKey.Length > 200)
            throw new ArgumentException("A refresh token of at most 200 characters is required.");
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private AuthResponse CreateAuthResponse(User user)
    {
        var jwtSettings = configuration.GetSection("Jwt");

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            Token = JwtHelper.GenerateToken(
                user,
                jwtSettings["SecretKey"]!,
                jwtSettings["Issuer"]!,
                jwtSettings["Audience"]!,
                int.Parse(jwtSettings["ExpireMinutes"] ?? "15"))
        };
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string RegistrationKey(string email) =>
        $"sportscenter:otp:register:{email}";

    private static string ResetPasswordKey(string email) =>
        $"sportscenter:otp:reset:{email}";

    private static string GenerateOtp() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6");

    private static string HashOtp(string otp) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

    private static BusinessException InvalidOtpException() =>
        new(
            StatusCodes.Status400BadRequest,
            "OTP is invalid or expired.");
}
