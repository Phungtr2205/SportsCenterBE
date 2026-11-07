using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;

namespace SportsCenterAPI.Services.Implement;

public class SupabaseImageStorage(HttpClient http, IOptions<SupabaseStorageConfig> options) : IImageStorage
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string> Types = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    public async Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.Url) || string.IsNullOrWhiteSpace(cfg.ServiceKey))
            throw new BusinessException(500, "Supabase Storage chưa được cấu hình.");
        if (file.Length is 0 or > MaxBytes)
            throw new BusinessException(400, "Ảnh phải có dung lượng từ 1 byte đến 2MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Types.TryGetValue(ext, out var contentType))
            throw new BusinessException(400, "Chỉ chấp nhận ảnh .jpg, .png, .webp.");

        var path = $"{folder}/{Guid.NewGuid():N}{ext}";
        var baseUrl = cfg.Url.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/storage/v1/object/{cfg.Bucket}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ServiceKey);
        await using var stream = file.OpenReadStream();
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BusinessException(502, "Upload ảnh lên Supabase thất bại.");

        return $"{baseUrl}/storage/v1/object/public/{cfg.Bucket}/{path}";
    }
}
