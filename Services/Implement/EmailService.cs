using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;

namespace SportsCenterAPI.Services.Implement;

public class EmailService : IEmailService
{
    private readonly MailSettings _mailSettings;

    public EmailService(IOptions<MailSettings> mailSettings)
    {
        _mailSettings = mailSettings.Value;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        string? htmlBody = null)
    {
        var email = new MimeMessage();

        email.From.Add(new MailboxAddress(
            _mailSettings.SenderName,
            _mailSettings.SenderEmail));

        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        // HTML để hiển thị đẹp, chữ thuần dùng làm bản dự phòng.
        email.Body = new BodyBuilder
        {
            TextBody = body,
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            _mailSettings.Host,
            _mailSettings.Port,
            SecureSocketOptions.StartTls);

        await smtp.AuthenticateAsync(
            _mailSettings.SenderEmail,
            _mailSettings.Password);

        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}