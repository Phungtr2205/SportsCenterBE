# Sports Center Management System — Backend

Backend quản lý trung tâm thể thao, phát triển bằng **ASP.NET Core Web API (.NET 10)**. Hệ thống hỗ trợ quản lý hội viên, gói tập, lớp học, lịch tập, thanh toán và báo cáo; đồng thời có các chức năng điểm danh và kế hoạch tập luyện.

Repository: [phcgnguyen19/SportsCenterBE](https://github.com/phcgnguyen19/SportsCenterBE).

## 1. Phạm vi chức năng

| Flow | Nội dung | Phạm vi đề tài |
| --- | --- | --- |
| Flow 1 | Quản lý tài khoản, nhân viên, hội viên và gói thành viên | Bắt buộc |
| Flow 2 | Quản lý lớp, lịch học, đặt và hủy đăng ký | Bắt buộc |
| Flow 3 | Thanh toán, xuất biên lai PDF và báo cáo | Bắt buộc |
| Flow 4 | Check-in trung tâm, điểm danh lớp và kế hoạch tập luyện | Tùy chọn; đã triển khai một phần |

### Vai trò

| Vai trò trong hệ thống | Quyền chính |
| --- | --- |
| `Manager` | Quản lý nhân viên, hội viên, bộ môn, phòng, lớp, lịch học, gói tập và báo cáo |
| `Receptionist` | Tiếp nhận hội viên tại quầy, đăng ký gói, xác nhận thu tiền, hỗ trợ đặt/hủy lớp và check-in |
| `Coach` | Xem lịch dạy, danh sách học viên, điểm danh buổi phụ trách và quản lý giáo án |
| `Member` | Quản lý hồ sơ cá nhân, đăng ký gói, thanh toán, đặt/hủy lớp và xem thông tin tập luyện |

Quyền thực tế được kiểm tra tại controller và các service tương ứng. Một số API xem danh mục cho phép truy cập không cần đăng nhập.

## 2. Công nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Backend | ASP.NET Core Web API, .NET 10 |
| Database | PostgreSQL; có thể sử dụng Supabase PostgreSQL |
| Truy cập dữ liệu | Entity Framework Core 10, Npgsql |
| Xác thực | JWT Bearer, refresh token |
| Mật khẩu | BCrypt.Net-Next |
| Email OTP | MailKit, MimeKit; mẫu HTML |
| Thanh toán online | payOS SDK |
| Biên lai PDF | QuestPDF |
| Tài liệu API | Swagger UI, Swashbuckle |

## 3. Cấu trúc dự án

```text
SportsCenterBE/
├── Controllers/             # HTTP endpoints và phân quyền
├── Services/
│   ├── Interface/           # Hợp đồng các service
│   └── Implement/           # Xử lý nghiệp vụ
├── DTOs/                    # Dữ liệu request và response
├── Models/                  # Các entity trong database
├── Data/                    # AppDbContext và dữ liệu khởi tạo
├── Migrations/              # EF Core migrations
├── Helpers/                 # JWT, mật khẩu, phân trang, lỗi nghiệp vụ...
├── Middleware/              # Xử lý lỗi tập trung
├── Settings/                # Cấu hình mail và payOS
├── Properties/              # Các profile chạy local
├── Program.cs               # Đăng ký DI, xác thực và middleware
└── SportsCenterAPI.csproj
```

## 4. Chạy local

### Yêu cầu

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- Git.
- PostgreSQL hoặc kết nối Supabase PostgreSQL.
- Tài khoản SMTP hỗ trợ STARTTLS để thử chức năng OTP.
- Thông tin tích hợp payOS để thử thanh toán online.
- Visual Studio hỗ trợ .NET 10 hoặc IDE/editor tương đương.

### Bước 1 — Lấy source

```powershell
git clone https://github.com/phcgnguyen19/SportsCenterBE.git
cd SportsCenterBE
dotnet restore
```

### Bước 2 — Cấu hình

Dự án đã có `UserSecretsId`. Khi phát triển local, có thể mở **Manage User Secrets** trên project trong Visual Studio và cấu hình theo mẫu dưới đây. Thay tất cả giá trị mẫu bằng thông tin của môi trường chạy.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_POSTGRES_HOST;Port=5432;Database=postgres;Username=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;SSL Mode=Require"
  },
  "Jwt": {
    "SecretKey": "REPLACE_WITH_A_RANDOM_SECRET_OF_AT_LEAST_32_BYTES",
    "Issuer": "SportsCenterAPI",
    "Audience": "SportsCenterClient",
    "ExpireMinutes": 60,
    "RefreshTokenExpireDays": 7
  },
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "SenderName": "Sports Center",
    "SenderEmail": "YOUR_SENDER_EMAIL",
    "Password": "YOUR_SMTP_APP_PASSWORD"
  },
  "PayOS": {
    "ClientId": "YOUR_PAYOS_CLIENT_ID",
    "ApiKey": "YOUR_PAYOS_API_KEY",
    "ChecksumKey": "YOUR_PAYOS_CHECKSUM_KEY",
    "ReturnUrl": "http://localhost:3000/payment/success",
    "CancelUrl": "http://localhost:3000/payment/cancel"
  }
}
```

- Với Supabase, dùng host, port, username và password từ phần **Connect** của đúng project. Chuỗi kết nối phải theo định dạng Npgsql ở trên.
- `SecretKey` dùng để ký JWT; giữ ổn định giữa các lần chạy nếu muốn các token đã cấp tiếp tục hợp lệ.
- SMTP hiện sử dụng `SecureSocketOptions.StartTls`. Với Gmail, sử dụng mật khẩu ứng dụng theo cấu hình tài khoản gửi.
- `ReturnUrl` và `CancelUrl` trỏ đến trang frontend. Các giá trị trên là ví dụ cho frontend ở cổng `3000`.
- User Secrets được nạp trong môi trường `Development`. Khi deploy, cấu hình bằng biến môi trường của nền tảng hosting.
- Không đưa mật khẩu DB, SMTP, JWT secret hoặc khóa payOS thật vào README hay commit mới.

Xem thêm: [ASP.NET Core User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

### Bước 3 — Build và chạy

```powershell
dotnet build
dotnet dev-certs https --trust
