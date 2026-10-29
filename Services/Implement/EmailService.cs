using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;

namespace SportsCenterAPI.Services.Implement;

public class EmailService(
    HttpClient http,
    IOptions<MailSettings> mailSettings,
    IConfiguration configuration) : IEmailService
{
    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        string? htmlBody = null)
    {
        var apiKey = configuration["SendGrid:ApiKey"]?.Trim();
        var sender = mailSettings.Value;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(sender.SenderEmail))
            throw new BusinessException(503,
                "Chưa cấu hình SendGrid:ApiKey và MailSettings:SenderEmail.");

        var content = new List<object>
        {
            new { type = "text/plain", value = body }
        };
        if (!string.IsNullOrWhiteSpace(htmlBody))
            content.Add(new { type = "text/html", value = htmlBody });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            personalizations = new[] { new { to = new[] { new { email = to } } } },
            from = new { email = sender.SenderEmail, name = sender.SenderName },
            subject,
            content
        });

        try
        {
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = response.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ? 503 : 502;

                // Không đưa response của nhà cung cấp, địa chỉ email hoặc API key vào lỗi trả cho client.
                throw new BusinessException(statusCode,
                    $"SendGrid từ chối gửi email (HTTP {(int)response.StatusCode}). " +
                    "Kiểm tra API key, quyền Mail Send, người gửi đã xác minh và hạn mức gửi.");
            }
        }
        catch (OperationCanceledException)
        {
            throw new BusinessException(504, "Gửi email qua SendGrid quá thời gian chờ. Vui lòng thử lại.");
        }
        catch (HttpRequestException)
        {
            throw new BusinessException(502, "Không kết nối được SendGrid. Vui lòng thử lại sau.");
        }
    }
}
