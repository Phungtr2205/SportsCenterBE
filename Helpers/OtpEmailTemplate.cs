using System.Net;

namespace SportsCenterAPI.Helpers;

public static class OtpEmailTemplate
{
    public static string Create(string otp, bool resetPassword)
    {
        string title = resetPassword
            ? "Đặt lại mật khẩu"
            : "Xác nhận đăng ký";

        string description = resetPassword
            ? "Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu tài khoản Sports Center của bạn. Nhập mã bên dưới để tiếp tục."
            : "Cảm ơn bạn đã đăng ký Sports Center. Nhập mã bên dưới để xác nhận email và hoàn tất đăng ký.";

        string safeOtp = WebUtility.HtmlEncode(otp);

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>{{title}} | Sports Center</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f2f5f8;font-family:Arial,Helvetica,sans-serif;color:#243447;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f2f5f8;">
                <tr>
                    <td align="center" style="padding:32px 12px;">
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                               style="max-width:560px;background-color:#ffffff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;">
                            <tr>
                                <td style="padding:28px 32px;background-color:#102b3f;border-bottom:4px solid #13a89e;">
                                    <div style="font-size:23px;font-weight:bold;letter-spacing:1px;color:#ffffff;">
                                        SPORTS CENTER
                                    </div>
                                    <div style="margin-top:7px;font-size:12px;line-height:18px;color:#b7ced9;">
                                        Tập luyện hôm nay. Khỏe hơn mỗi ngày.
                                    </div>
                                </td>
                            </tr>

                            <tr>
                                <td style="padding:32px;">
                                    <div style="font-size:12px;font-weight:bold;letter-spacing:1px;color:#11857d;">
                                        BẢO MẬT TÀI KHOẢN
                                    </div>

                                    <h1 style="margin:12px 0 16px;font-size:28px;line-height:36px;color:#102b3f;">
                                        {{title}}
                                    </h1>

                                    <p style="margin:0 0 24px;font-size:15px;line-height:25px;color:#526477;">
                                        Xin chào,<br>
                                        {{description}}
                                    </p>

                                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                                           style="background-color:#eef8f6;border:1px solid #cce9e4;border-radius:8px;">
                                        <tr>
                                            <td align="center" style="padding:22px 10px;">
                                                <div style="font-size:11px;font-weight:bold;letter-spacing:1px;color:#46766f;">
                                                    MÃ XÁC NHẬN CỦA BẠN
                                                </div>

                                                <div style="margin:12px 0;font-family:Consolas,'Courier New',monospace;font-size:36px;line-height:44px;font-weight:bold;letter-spacing:6px;color:#102b3f;">
                                                    {{safeOtp}}
                                                </div>

                                                <div style="font-size:13px;line-height:20px;color:#526477;">
                                                    Có hiệu lực trong <strong>5 phút</strong>
                                                </div>
                                            </td>
                                        </tr>
                                    </table>

                                    <p style="margin:24px 0 0;font-size:13px;line-height:22px;color:#526477;">
                                        Không chia sẻ mã này với bất kỳ ai, kể cả người tự nhận là nhân viên hỗ trợ.
                                    </p>

                                    <p style="margin:12px 0 0;font-size:13px;line-height:22px;color:#526477;">
                                        Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email.
                                        Không có thay đổi nào được thực hiện chỉ bằng việc gửi mã.
                                    </p>
                                </td>
                            </tr>

                            <tr>
                                <td style="padding:20px 32px;background-color:#f8fafc;border-top:1px solid #e2e8f0;">
                                    <div style="font-size:12px;font-weight:bold;color:#102b3f;">
                                        Sports Center
                                    </div>
                                    <div style="margin-top:6px;font-size:12px;line-height:20px;color:#718096;">
                                        Email được gửi tự động. Vui lòng không trả lời.
                                    </div>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }
}