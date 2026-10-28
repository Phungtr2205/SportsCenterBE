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
| Flow 5 | AI gợi ý bài tập từ mục tiêu, trình độ và dữ liệu tập luyện | Tùy chọn; API tư vấn |
| Flow 6 | AI giải đáp về gói tập và lịch cá nhân | Tùy chọn; API tư vấn |

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
| Lưu OTP | IMemoryCache; hết hạn sau 5 phút |
| AI | GroqCloud Responses API qua HttpClient |
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

`appsettings.json` trong Git để trống các trường bí mật. Mỗi máy cấu hình User Secrets khi chạy Visual Studio hoặc `dotnet run`, hoặc `.env` khi chạy Docker Compose. Không cần dịch vụ lưu OTP bên ngoài.

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
  "AI": {
    "ApiKey": "YOUR_GROQ_API_KEY",
    "Model": "openai/gpt-oss-20b"
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
dotnet run
```

Mở URL từ console và thêm `/swagger`. Với profile HTTPS hiện tại: `https://localhost:7182/swagger`.

Ứng dụng mặc định chạy migration và seed dữ liệu khi khởi động. Đặt `Database:InitializeOnStartup=false` nếu database đã được chuẩn bị và không muốn thực hiện bước này. API key AI có thể để trống; hai API AI trả `503` cho đến khi được cấu hình.

## 5. Chạy bằng Docker

Nếu chưa có `.env`, copy `.env.example` thành `.env` và điền PostgreSQL, JWT, SMTP cùng các dịch vụ cần dùng. Nếu đã có `.env`, giữ file hiện tại và kiểm tra các giá trị trong đó.

```powershell
Copy-Item .env.example .env
docker compose up -d --build
```

Mở `http://localhost:8081/swagger`. Compose ánh xạ cổng `8081` trên máy vào cổng `8080` trong container và chỉ chạy backend; OTP lưu trong bộ nhớ của backend. Database vẫn dùng PostgreSQL/Supabase theo chuỗi kết nối được cấu hình. Nếu PostgreSQL chạy trên máy Windows, dùng host `host.docker.internal` trong container.

Compose mặc định đặt `Database__InitializeOnStartup=false` để không tự chạy migration hoặc seed lên database hiện có. Chỉ đặt thành `true` trong `.env` khi chủ động muốn chạy bước này. Backend vẫn cần chuỗi kết nối database để xử lý API.

```powershell
docker compose logs -f api
docker compose down
```

`.env` không được commit hoặc đưa vào image. ASP.NET Core không tự đọc `.env`; Compose nạp các biến vào container. Khi bấm F5 trong Visual Studio, dùng User Secrets như phần chạy local. OTP mất khi restart hoặc tạo lại container; cấu hình này dành cho một instance backend.

Dockerfile hỗ trợ biến `PORT` (mặc định `8080`). Nếu deploy bằng Docker trên Render, cấu hình các biến trong `.env.example` trên service và đặt `OpenApi__Enabled=true` nếu cần Swagger. `ReturnUrl`/`CancelUrl` của payOS cần trỏ đến frontend đang chạy.

## 6. Hai flow AI

AI dùng [GroqCloud Responses API](https://console.groq.com/docs/responses-api) tại `https://api.groq.com/openai/v1/responses`. Tạo key tại [GroqCloud API Keys](https://console.groq.com/keys), rồi cấu hình khi chạy Visual Studio/`dotnet run`:

```powershell
dotnet user-secrets set "AI:ApiKey" "YOUR_GROQ_API_KEY"
dotnet user-secrets set "AI:Model" "openai/gpt-oss-20b"
```

Thay giá trị mẫu bằng key thật. Khi deploy, dùng `AI__ApiKey` và `AI__Model` thay cho User Secrets. Model `openai/gpt-oss-20b` có trong [danh sách model Groq](https://console.groq.com/docs/models); tên có `openai/` nhưng request gửi tới GroqCloud. Responses API của Groq hiện ở beta. Không gửi key vào Swagger hoặc commit vào repo.

Đăng nhập qua `POST /api/Auth/login`, lấy `token`, nhập vào nút **Authorize** trong Swagger.

**Flow 5 — `POST /api/ai/workout-recommendations`** (`Member`, `Coach`):

```json
{
  "level": "Beginner",
  "daysPerWeek": 3,
  "goal": "Tăng sức bền"
}
```

Member bỏ `memberId` để dùng hồ sơ của mình. Coach phải thêm `memberId` của học viên mình phụ trách qua buổi học hoặc giáo án. `level` nhận `Beginner`, `Intermediate`, `Advanced`; số ngày từ 1 đến 7. Nếu bỏ `goal`, service dùng mục tiêu trong hồ sơ.

Service kiểm tra quyền → đọc tối đa 5 bài trong giáo án và 5 bản ghi điểm danh → gửi dữ liệu cho GroqCloud → trả `data.answer`. Giáo án không được coi là bằng chứng học viên đã hoàn thành bài tập.

**Flow 6 — `POST /api/ai/chat`** (`Member`):

```json
{
  "question": "Gói tập của tôi còn hạn không và lịch học sắp tới là gì?"
}
```

Service đọc tối đa 20 gói đang mở, 5 đăng ký gói gần nhất và 10 buổi học sắp tới của chính member. Dữ liệu này được gửi như thông tin tham khảo; câu hỏi là tin nhắn user riêng ở cuối. AI trả lời trực tiếp câu hỏi, chỉ dùng dữ liệu gói/lịch khi liên quan. AI chỉ tư vấn; không tự đặt lớp, thanh toán hoặc lưu giáo án. Câu trả lời AI và lịch sử chat chưa được lưu trong database.

Hai endpoint dùng GroqCloud Responses API với `store=false`. Dữ liệu mục tiêu, giáo án/nhận xét, điểm danh hoặc lịch/gói cá nhân có thể được gửi đến GroqCloud theo endpoint; không gửi mật khẩu hay JWT. Chưa cấu hình AI trả `503`; sai quyền trả `403`; học viên không thuộc phạm vi trả `404`; phản hồi AI lỗi trả `502`; hết thời gian chờ trả `504`. Hạn mức và phí của dịch vụ AI phụ thuộc tài khoản GroqCloud.

## 7. OTP lưu trong bộ nhớ

Endpoint và JSON đăng ký/quên mật khẩu giữ nguyên. `MemoryOtpStore` dùng `IMemoryCache` để lưu mã đã băm SHA-256, dữ liệu chờ và số lần sai trong 5 phút. Mật khẩu đăng ký được băm BCrypt trước khi lưu dữ liệu chờ. Kiểm tra mã và xóa dữ liệu được bảo vệ bằng khóa chung trong một singleton, nên các request đồng thời trong cùng backend không thể dùng thành công cùng một mã.

Sai 5 lần hoặc hết hạn phải xin mã mới; gửi lại thay dữ liệu chờ cũ và đặt lại thời hạn. OTP đúng được dùng một lần trước khi tạo tài khoản/đổi mật khẩu; nếu bước database sau đó lỗi, cần xin OTP mới.

Chạy trực tiếp bằng Visual Studio hoặc `dotnet run`. OTP mất khi khởi động lại backend; cấu hình này dành cho một instance backend. Khi triển khai nhiều instance, cần thay kho OTP bằng một kho dùng chung.
