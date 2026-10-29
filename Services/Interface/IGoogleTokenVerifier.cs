namespace SportsCenterAPI.Services.Interface;

public record GoogleUserInfo(string Subject, string Email, string FullName);

public interface IGoogleTokenVerifier
{
    /// <summary>Xác thực Google ID token và trả về thông tin người dùng.</summary>
    Task<GoogleUserInfo> VerifyIdTokenAsync(string idToken);

    /// <summary>Đổi authorization code lấy ID token rồi xác thực.</summary>
    Task<GoogleUserInfo> ExchangeCodeAsync(string code, string redirectUri);
}
