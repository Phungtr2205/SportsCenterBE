namespace SportsCenterAPI.Services.Interface;

public interface IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body,string? htmlBody = null)
    {
        // Giữ phần xử lý gửi mail bên trong.
    }
}