namespace SportsCenterAPI.Services.Interface;

public interface IImageStorage
{
    /// <summary>Upload ảnh lên Supabase Storage, trả về public URL.</summary>
    Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken);
}
