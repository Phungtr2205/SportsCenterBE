using System.Text.Json.Serialization;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;

namespace SportsCenterAPI.Services.Implement;

public class GoogleTokenVerifier(
    HttpClient httpClient,
    IOptions<GoogleAuthConfig> options) : IGoogleTokenVerifier
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private readonly GoogleAuthConfig config = options.Value;

    private sealed class TokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }

    public async Task<GoogleUserInfo> VerifyIdTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId))
            throw new BusinessException(
                StatusCodes.Status503ServiceUnavailable,
                "Google login is not configured.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [config.ClientId]
                });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Google token is invalid.");
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            throw new UnauthorizedAccessException("Google email is not verified.");

        return new GoogleUserInfo(
            payload.Subject,
            payload.Email,
            string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name);
    }

    public async Task<GoogleUserInfo> ExchangeCodeAsync(string code, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId) ||
            string.IsNullOrWhiteSpace(config.ClientSecret))
            throw new BusinessException(
                StatusCodes.Status503ServiceUnavailable,
                "Google login is not configured.");

        using var response = await httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }));

        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Google authorization code is invalid.");

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (string.IsNullOrWhiteSpace(token?.IdToken))
            throw new UnauthorizedAccessException("Google did not return an ID token.");

        return await VerifyIdTokenAsync(token.IdToken);
    }
}
