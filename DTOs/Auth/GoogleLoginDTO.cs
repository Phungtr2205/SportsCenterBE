using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Auth
{
    /// <summary>
    /// Gửi IdToken (Google Identity Services) hoặc Code + RedirectUri (authorization code flow).
    /// </summary>
    public class GoogleLoginDTO
    {
        [StringLength(4096)]
        public string? IdToken { get; set; }

        [StringLength(2048)]
        public string? Code { get; set; }

        [StringLength(2048)]
        public string? RedirectUri { get; set; }
    }
}
