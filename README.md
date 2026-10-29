# Sports Center Management System — Backend

Backend cho hệ thống quản lý trung tâm thể thao, xây dựng bằng **ASP.NET Core Web API (.NET 10)**. Hệ thống phục vụ quản lý tài khoản, hội viên, gói tập, lớp học, lịch học, thanh toán và báo cáo; đồng thời hỗ trợ điểm danh, giáo án và tư vấn AI.

Repository: [phcgnguyen19/SportsCenterBE](https://github.com/phcgnguyen19/SportsCenterBE).

## Mục lục

- [Kiến trúc hiện tại](#kiến-trúc-hiện-tại)
- [Phạm vi và vai trò](#phạm-vi-và-vai-trò)
- [Công nghệ](#công-nghệ)
- [Cấu trúc repository](#cấu-trúc-repository)
- [Yêu cầu môi trường](#yêu-cầu-môi-trường)
- [Khởi chạy local](#khởi-chạy-local)
- [Chạy bằng Docker Compose](#chạy-bằng-docker-compose)
- [Cấu hình](#cấu-hình)
- [API và Swagger](#api-và-swagger)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)
- [Dữ liệu và OTP](#dữ-liệu-và-otp)
- [Kiểm thử](#kiểm-thử)
- [Quy ước phát triển](#quy-ước-phát-triển)
- [Giới hạn hiện tại](#giới-hạn-hiện-tại)

## Kiến trúc hiện tại

Dự án là **layered monolith**: các chức năng nằm trong một ứng dụng Web API và được triển khai chung. Cấu trúc chính là `Controller → Service → AppDbContext → PostgreSQL`.

```text
Web / Mobile / Swagger
          |
          v
  SportsCenterAPI (.NET 10)
  ├── Controllers            Nhận request, phân quyền, trả response
  ├── Services               Xử lý nghiệp vụ
  └── AppDbContext           Truy cập dữ liệu bằng EF Core
          |
          +----> PostgreSQL / Supabase
          +----> SendGrid HTTPS API: gửi email và OTP
          +----> payOS: tạo link và đồng bộ thanh toán
          +----> GroqCloud: tư vấn và gợi ý bài tập
```

Một số controller vẫn truy cập `AppDbContext` trực tiếp, chẳng hạn phần xuất biên lai và xem audit log. Việc chia tầng hiện chưa được áp dụng đồng đều cho mọi endpoint.

`PaymentService` là lớp nghiệp vụ bên trong API. Docker Compose hiện chỉ chạy **một container API**; PostgreSQL/Supabase là kết nối được cấu hình bên ngoài. OTP dùng bộ nhớ của API, không cần Redis ở phiên bản hiện tại.

## Phạm vi và vai trò

| Flow | Nội dung | Phạm vi đề tài |
|---|---|---|
| 1 | Tài khoản, hội viên, nhân viên, hồ sơ và gói thành viên | Bắt buộc |
| 2 | Lớp học, lịch học, phòng tập, đặt và hủy đăng ký | Bắt buộc |
| 3 | Thanh toán, lịch sử giao dịch, biên lai PDF và báo cáo | Bắt buộc |
| 4 | Check-in trung tâm, điểm danh lớp, giáo án và kết quả tập luyện | Tùy chọn; đã có các API hỗ trợ |
| 5 | AI gợi ý bài tập theo mục tiêu, trình độ và dữ liệu tập luyện | Tùy chọn; đã có API tư vấn |
| 6 | AI giải đáp câu hỏi về tập luyện, gói tập và lịch cá nhân | Tùy chọn; đã có API tư vấn |

Bảng trên mô tả chức năng có trong source; không thay thế kết quả kiểm thử toàn bộ nghiệp vụ.

| Vai trò | Giá trị trong code | Chức năng chính |
|---|---|---|
| Quản lý trung tâm | `Manager` | Quản lý nhân viên, hội viên, danh mục, gói tập, lớp, phòng, lịch, báo cáo và audit log |
| Lễ tân | `Receptionist` | Tiếp nhận hội viên tại quầy, hỗ trợ đăng ký gói/lớp, ghi nhận thanh toán và check-in |
| Huấn luyện viên | `Coach` | Xem lịch dạy, danh sách học viên, điểm danh và quản lý giáo án |
| Hội viên | `Member` | Quản lý hồ sơ, đăng ký gói, thanh toán, đặt/hủy lớp và xem thông tin tập luyện |

Quyền cụ thể phụ thuộc từng endpoint và kiểm tra trong service. `UserRoles.FrontDesk` là nhóm quyền gồm `Manager,Receptionist`, không phải một vai trò riêng trong database.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | C#, ASP.NET Core Web API, .NET 10 |
| Database | PostgreSQL; có thể kết nối Supabase PostgreSQL |
| Truy cập dữ liệu | Entity Framework Core 10, Npgsql, EF migrations |
| Xác thực | JWT Bearer, refresh token, BCrypt.Net-Next |
| OTP | `IMemoryCache`, `MemoryOtpStore` |
| Email | SendGrid Web API qua HttpClient (HTTPS) |
| Thanh toán | payOS SDK |
| Biên lai PDF | QuestPDF |
| AI | GroqCloud Responses API qua `HttpClient` |
| Tài liệu API | Swagger UI, Swashbuckle, XML comments |
| Kiểm thử | xUnit, Moq, EF Core InMemory, Coverlet |
| Đóng gói | Docker, Docker Compose |
| CI | GitHub Actions: restore, build và test solution |

## Cấu trúc repository

```text
SportsCenterBE/
├── Controllers/                 # HTTP endpoints, phân quyền
├── Services/
│   ├── Interface/               # Hợp đồng service
│   └── Implement/               # Xử lý nghiệp vụ
├── DTOs/                        # Request và response theo chức năng
├── Models/                      # Entity database
├── Data/                        # AppDbContext, DbSeeder
├── Migrations/                  # EF Core migrations
├── Enums/                       # Trạng thái nghiệp vụ
├── Helpers/                     # JWT, mật khẩu, phân trang, lỗi nghiệp vụ...
├── Middleware/                  # Xử lý exception tập trung
├── Settings/                    # Cấu hình email, payOS
├── Properties/                  # Profile chạy local
├── tests/SportsCenterAPI.Tests/  # Unit tests
├── .github/workflows/           # GitHub Actions
├── wwwroot/                     # Thư mục nội dung tĩnh
├── .env.example                 # Mẫu cấu hình cho Compose
├── .dockerignore
├── .gitignore
├── Dockerfile
├── compose.yaml
├── Program.cs                   # DI, authentication, middleware, startup
├── SportsCenterAPI.csproj
└── SportsCenterAPI.slnx
```

## Yêu cầu môi trường

- Git và .NET SDK 10 khi chạy trực tiếp hoặc phát triển code.
- PostgreSQL hoặc chuỗi kết nối đến Supabase PostgreSQL.
- Docker Desktop/Engine có Compose nếu chọn chạy bằng container.
- Tài khoản SendGrid, API key có quyền Mail Send và địa chỉ gửi đã xác minh để kiểm thử OTP; thông tin payOS để kiểm thử thanh toán online; API key GroqCloud để dùng AI.

| Cách chạy | Địa chỉ Swagger |
|---|---|
| Profile `https` | `https://localhost:7182/swagger` |
| Profile `http` | `http://localhost:5212/swagger` |
| Docker Compose | `http://localhost:8081/swagger` |

Các cổng tương ứng phải đang trống. Docker Compose không yêu cầu cài .NET SDK trên máy host vì image SDK thực hiện build.

## Khởi chạy local

### 1. Lấy source

```powershell
git clone https://github.com/phcgnguyen19/SportsCenterBE.git
cd SportsCenterBE
dotnet restore SportsCenterAPI.slnx
```

### 2. Cấu hình User Secrets

Dự án đã có `UserSecretsId`. Trong Visual Studio, chọn **Manage User Secrets** trên project `SportsCenterAPI`, rồi điền cấu hình sau bằng giá trị của môi trường chạy:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_POSTGRES_HOST;Port=5432;Database=postgres;Username=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;SSL Mode=Require"
  },
  "Database": {
    "InitializeOnStartup": false
  },
  "Jwt": {
    "SecretKey": "REPLACE_WITH_A_RANDOM_SECRET_OF_AT_LEAST_32_BYTES",
    "Issuer": "SportsCenterAPI",
    "Audience": "SportsCenterClient",
    "ExpireMinutes": 15,
    "RefreshTokenExpireDays": 7
  },
  "MailSettings": {
    "SenderName": "Sports Center",
    "SenderEmail": "YOUR_VERIFIED_SENDER_EMAIL"
  },
  "SendGrid": {
    "ApiKey": "YOUR_SENDGRID_API_KEY"
  },
  "PayOS": {
    "ClientId": "YOUR_PAYOS_CLIENT_ID",
    "ApiKey": "YOUR_PAYOS_API_KEY",
    "ChecksumKey": "YOUR_PAYOS_CHECKSUM_KEY",
    "ReturnUrl": "http://localhost:3000/payment/success",
    "CancelUrl": "http://localhost:3000/payment/cancel"
  },
  "AI": {
    "ApiKey": "",
    "Model": "openai/gpt-oss-20b"
  }
}
```

Với Supabase, lấy host, port và username từ phần **Connect** của đúng project. API dùng chuỗi kết nối định dạng **Npgsql** như ví dụ, không dùng nguyên URI `postgresql://...` làm giá trị cấu hình.

`InitializeOnStartup=false` dành cho database đã được chuẩn bị. Với database local mới, đặt `true` để ứng dụng chạy migration và seed lúc khởi động. Khi không khai báo khóa này, code hiện mặc định `true`; kiểm tra cấu hình trước khi trỏ vào database dùng chung.

User Secrets được nạp khi chạy môi trường `Development`. File `.env` của Docker Compose không tự được đọc khi bấm F5 hoặc dùng `dotnet run`.

### 3. Build và chạy

```powershell
dotnet build SportsCenterAPI.slnx
dotnet dev-certs https --trust
dotnet run --project SportsCenterAPI.csproj --launch-profile https
```

Mở `https://localhost:7182/swagger`. Có thể dùng profile HTTP nếu không cần HTTPS local:

```powershell
dotnet run --project SportsCenterAPI.csproj --launch-profile http
```

## Chạy bằng Docker Compose

Tại thư mục gốc repo, nếu chưa có `.env`:

```powershell
Copy-Item .env.example .env
```

Điền các giá trị trong `.env`, sau đó chạy:

```powershell
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

Mở `http://localhost:8081/swagger`. Compose ánh xạ `127.0.0.1:8081` của máy host vào cổng `8080` của container. Database vẫn là PostgreSQL/Supabase được chỉ định trong `.env`. Nếu PostgreSQL chạy trên máy host Docker Desktop, có thể dùng `host.docker.internal` làm host kết nối.

Compose mặc định `Database__InitializeOnStartup=false`. Chỉ bật `true` khi muốn chạy migration và seed. Để dừng container:

```powershell
docker compose down
```

Dockerfile dùng build nhiều giai đoạn, chạy ứng dụng bằng user không phải root và hỗ trợ biến `PORT` (mặc định `8080`). Khi deploy Docker lên hosting như Render, khai báo biến môi trường trên service; `.env` local không tự được chuyển lên hosting.

## Cấu hình

ASP.NET Core dùng `:` trong cấu hình JSON/User Secrets và `__` trong biến môi trường. Ví dụ `AI:ApiKey` tương ứng `AI__ApiKey`.

| Biến môi trường | Ý nghĩa |
|---|---|
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối PostgreSQL/Npgsql |
| `Database__InitializeOnStartup` | Bật/tắt migration và seed khi khởi động |
| `Jwt__SecretKey` | Secret ký và xác thực JWT |
| `Jwt__Issuer`, `Jwt__Audience` | Issuer và audience hợp lệ |
| `Jwt__ExpireMinutes` | Thời hạn access token |
| `Jwt__RefreshTokenExpireDays` | Thời hạn refresh token |
| `SendGrid__ApiKey` | API key SendGrid có quyền Mail Send |
| `MailSettings__SenderName`, `MailSettings__SenderEmail` | Tên và địa chỉ người gửi đã xác minh trên SendGrid |
| `PayOS__ClientId`, `PayOS__ApiKey`, `PayOS__ChecksumKey` | Thông tin tích hợp payOS |
| `PayOS__ReturnUrl`, `PayOS__CancelUrl` | Trang frontend sau thanh toán/hủy |
| `AI__ApiKey`, `AI__Model` | API key và model GroqCloud |
| `OpenApi__Enabled` | Bật Swagger ngoài môi trường Development |
| `ASPNETCORE_ENVIRONMENT` | Môi trường chạy ASP.NET Core |
| `PORT` | Cổng lắng nghe khi dùng Dockerfile |

Không commit `.env`, mật khẩu, API key hay token thật. Thành viên mới cần tự cấu hình User Secrets hoặc `.env`; chỉ pull source chưa đủ để kết nối các dịch vụ.

CORS hiện cho phép frontend `http://localhost:3000` trong `Program.cs`. Khi đổi domain frontend, cập nhật cấu hình này. Email dùng SendGrid Web API qua HTTPS cổng 443, phù hợp với Render Free đang chặn các cổng SMTP. Không dùng mật khẩu Gmail trong phần gửi mail này.

### Thiết lập SendGrid

1. Tạo tài khoản SendGrid và xác minh địa chỉ người gửi trong **Settings → Sender Authentication**. Với demo có thể dùng Single Sender Verification; triển khai chính thức nên xác thực domain.
2. Tạo API key có quyền **Mail Send**, lưu vào User Secrets dưới khóa `SendGrid:ApiKey` hoặc biến môi trường `SendGrid__ApiKey` trên Render.
3. Cấu hình `MailSettings:SenderEmail` đúng địa chỉ đã xác minh và `MailSettings:SenderName` theo tên trung tâm. Hai biến tương ứng trên Render là `MailSettings__SenderEmail`, `MailSettings__SenderName`.
4. Deploy lại rồi thử gửi OTP đăng ký, đặt lại mật khẩu và đổi email. Kiểm tra hộp thư spam và Email Activity trên SendGrid khi cần.

`EmailService` gọi `POST https://api.sendgrid.com/v3/mail/send` qua `HttpClient`, giữ cả nội dung chữ và HTML. Không cần thêm SendGrid SDK. Response `202 Accepted` nghĩa là SendGrid đã nhận yêu cầu, chưa đảm bảo email đã đến hộp thư.

Thiếu cấu hình trả `503`; SendGrid từ chối key/quyền hoặc giới hạn gửi (`401/403/429`) trả `503`; các lỗi HTTP khác hoặc mất kết nối trả `502`; quá thời gian chờ 30 giây trả `504`. Không tự retry để tránh gửi lặp. Logic tạo/xác thực OTP giữ nguyên. Khi đổi sang SendGrid, máy local cũng cần cấu hình key và người gửi mới. Các biến SMTP cũ có thể giữ lại nhưng không còn được đọc bởi `EmailService`.

Hạn mức và thời gian dùng thử phụ thuộc tài khoản SendGrid; Render Free không bao gồm phí email. Không commit hoặc chia sẻ API key trong ảnh chụp/chat.

Tài liệu: [Mail Send API](https://www.twilio.com/docs/sendgrid/api-reference/mail-send/mail-send), [Sender Identity](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/sender-identity).

## API và Swagger

Swagger là nơi xem đầy đủ endpoint, DTO, tham số và chú thích. Tài liệu OpenAPI nằm tại `/swagger/v1/swagger.json` khi Swagger được bật.

### Xác thực

Đăng nhập bằng `POST /api/Auth/login`, lấy trường `token`, chọn **Authorize** và nhập JWT trực tiếp, không thêm tiền tố `Bearer`. Swagger tự gắn header `Authorization`.

| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/Auth/send-register-otp` | Gửi OTP đăng ký với thông tin tài khoản |
| POST | `/api/Auth/verify-register-otp` | Xác thực OTP và tạo hội viên |
| POST | `/api/Auth/login` | Nhận access token và refresh token |
| POST | `/api/Auth/request-reset-password` | Yêu cầu OTP đặt lại mật khẩu |
| POST | `/api/Auth/verify-reset-password` | Xác thực OTP và đặt mật khẩu mới |
| POST | `/api/Auth/refresh-token` | Cấp access token mới và thay refresh token |
| POST | `/api/Auth/logout` | Thu hồi refresh token |

Refresh token được lưu dạng băm trong database. Khi refresh thành công, client thay token cũ bằng token mới; thời điểm hết hạn của phiên không được kéo dài. Đăng xuất thu hồi refresh token; access token đã cấp vẫn có thể còn hạn. API kiểm tra trạng thái tài khoản và vai trò hiện tại khi xác thực JWT.

### Nhóm API nghiệp vụ

| Nhóm | Đường dẫn chính | Chức năng |
|---|---|---|
| Hồ sơ | `/api/profile` | Xem/sửa hồ sơ, đổi email qua OTP |
| Nhân viên | `/api/users/staff` | Danh sách, tạo, đổi vai trò, ngừng hoạt động |
| Hội viên | `/api/members` | Đăng ký tại quầy, xem thông tin hội viên |
| Gói tập | `/api/membership-packages` | Xem gói công khai; quản lý gói |
| Đăng ký gói | `/api/member-subscriptions` | Tạo đăng ký, xem gói, hủy chờ thanh toán, xem trước nâng cấp |
| Bộ môn và HLV | `/api/sports`, `/api/coaches` | Quản lý danh mục, thông tin HLV |
| Lớp học | `/api/classes` | Quản lý lớp, tạo và cập nhật buổi học |
| Buổi học | `/api/class-sessions` | Tìm lịch, lịch dạy, đặt chỗ, danh sách học viên, hủy/hoàn tất buổi |
| Đăng ký lớp | `/api/class-registrations` | Lịch đã đăng ký, hủy, check-in và đánh giá |
| Chính sách hủy | `/api/cancellation-policies` | Quản lý thời hạn hủy |
| Phòng tập | `/api/Rooms` | Quản lý phòng, kiểm tra lịch trống |
| Thanh toán | `/api/payments` | Tạo, xem lịch sử/chi tiết, sync, hủy, webhook, xuất PDF |
| Báo cáo | `/api/Reports` | Tổng quan, doanh thu và tình trạng lớp |
| Check-in trung tâm | `/api/GymCheckIn` | QR, check-in/out và lịch sử |
| Điểm danh lớp | `/api/Attendances` | Điểm danh theo buổi, xem lịch sử cá nhân |
| Bài tập | `/api/Exercises` | Danh mục bài tập và nhóm cơ |
| Giáo án | `/api/TrainingPlans` | Tạo giáo án, xem theo hội viên, ghi kết quả và đổi trạng thái |
| Thông báo | `/api/notifications` | Xem và đánh dấu đã đọc |
| Hỗ trợ | `/api/SupportTickets` | Hội viên gửi yêu cầu; lễ tân/quản lý tiếp nhận và xử lý |
| Audit log | `/api/audit-logs` | Quản lý tra cứu lịch sử thao tác, lọc và phân trang |
| AI | `/api/ai` | Gợi ý bài tập và giải đáp câu hỏi |

Các đường dẫn trên là nhóm endpoint, không đại diện một HTTP method chung. Xem Swagger để biết quyền và request cụ thể. Endpoint hủy **đăng ký của hội viên** là `/api/class-registrations/{id}/cancel`; hủy **cả buổi học** là `/api/class-sessions/{id}/cancel`.

### Response

Nhiều endpoint dùng `ApiResponse<T>`:

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Thành công.",
  "data": {},
  "errors": null,
  "timestamp": "2026-10-29T00:00:00Z"
}
```

Middleware exception thường trả `{ "statusCode": 400, "message": "..." }`. Auth, lỗi validation, phản hồi 401/403 và endpoint trả file có thể có cấu trúc khác; frontend cần xử lý theo contract của từng endpoint. Không giả định mọi response đều có `data`.

## Luồng nghiệp vụ

### Flow 1 — Tài khoản và gói thành viên

Hội viên gửi thông tin đăng ký → nhận OTP qua email → xác thực OTP → đăng nhập → cập nhật hồ sơ → xem và đăng ký gói. Lễ tân/quản lý có API đăng ký hội viên và gói tại quầy; quản lý phụ trách tài khoản nhân viên và vai trò.

### Flow 2 — Lớp học và lịch học

Quản lý tạo bộ môn, HLV, phòng và lớp → tạo buổi học cụ thể → hội viên xem lịch và đặt chỗ → xem các đăng ký của mình → hủy đăng ký theo chính sách hoặc tham gia buổi học. HLV xem lịch dạy và danh sách học viên qua API riêng.

### Flow 3 — Thanh toán và báo cáo

Tạo đăng ký gói → tạo yêu cầu thanh toán → thanh toán qua payOS hoặc ghi nhận thu tiền tại quầy → cập nhật giao dịch và trạng thái gói → xem lịch sử/xuất biên lai → quản lý xem báo cáo.

Với payOS, frontend dùng link thanh toán được trả về. Backend nhận `POST /api/payments/payos/webhook` hoặc đồng bộ qua `POST /api/payments/{id}/sync`. Webhook không yêu cầu JWT nhưng dữ liệu phải qua xác minh chữ ký payOS. Khi triển khai, đăng ký URL webhook HTTPS công khai với payOS; localhost không thể nhận callback trực tiếp từ dịch vụ ngoài.

Biên lai PDF được tải qua `GET /api/payments/{id}/export-pdf` cho giao dịch đã hoàn tất. Trang return/cancel của frontend là trang điều hướng; trạng thái thanh toán cần lấy lại từ backend.

### Flow 4 — Điểm danh và tập luyện

Lễ tân/quản lý check-in trung tâm hoặc quét QR → HLV/nhân viên điểm danh lớp → HLV tạo giáo án cho hội viên → ghi nhận kết quả/nhận xét và cập nhật trạng thái giáo án → hội viên xem thông tin tập luyện. Check-in trung tâm và điểm danh từng lớp là hai nhóm dữ liệu riêng.

### Flow 5 và 6 — AI

API dùng GroqCloud Responses API tại `https://api.groq.com/openai/v1/responses`. Cấu hình `AI:ApiKey` và `AI:Model` trước khi thử.

**Gợi ý bài tập:** `POST /api/ai/workout-recommendations`, dành cho `Member` và `Coach`.

```json
{
  "goal": "Tăng sức bền",
  "level": "Beginner",
  "daysPerWeek": 3
}
```

Member dùng hồ sơ của mình; Coach cung cấp thêm `memberId` của học viên thuộc phạm vi được phép. `level` nhận `Beginner`, `Intermediate`, `Advanced`; `daysPerWeek` từ 1 đến 7.

Kết quả trả về trong `data` là JSON: `goal`, `level`, `daysPerWeek`, `sessions[]` (`day`, `title`, `durationMinutes`, `exercises[]` gồm `exerciseName`, `sets`, `reps`, `restSeconds`, `note`) và `warnings[]`. Nếu AI trả sai định dạng, API trả 502.

**Trợ lý AI:** `POST /api/ai/chat`, dành cho `Member`.

```json
{
  "question": "Gói tập của tôi còn hạn không và lịch học sắp tới là gì?"
}
```

Service lấy dữ liệu phù hợp, gửi cho GroqCloud và trả câu trả lời trong `data.answer`. AI chỉ tư vấn, không tự đăng ký lớp, thu tiền hoặc tạo giáo án trong database. Một phần dữ liệu mục tiêu, giáo án, điểm danh hoặc gói/lịch cá nhân được gửi cho nhà cung cấp AI tùy endpoint. Không gửi API key vào request Swagger.

## Dữ liệu và OTP

- PostgreSQL lưu tài khoản, refresh token dạng băm, hội viên, gói/đăng ký, lớp/buổi học, thanh toán, điểm danh, giáo án, thông báo, hỗ trợ và audit log.
- Schema được quản lý bằng EF Core migrations. `DbSeeder` có dữ liệu mẫu; kiểm tra nội dung trước khi bật seed trên database dùng chung.
- Gửi thời gian buổi học bằng ISO 8601 có múi giờ, ví dụ `2026-11-10T19:00:00+07:00`. Response có thể hiển thị UTC với hậu tố `Z`; frontend chuyển sang giờ hiển thị cho người dùng. Biên lai hiển thị UTC+7.
- OTP đăng ký/quên mật khẩu được lưu bằng singleton `MemoryOtpStore` và `IMemoryCache`, hết hạn sau 5 phút, tối đa 5 lần nhập sai. Mã được băm SHA-256; mật khẩu đăng ký được băm BCrypt trước khi lưu dữ liệu chờ.
- Mã đúng được tiêu thụ một lần trong cùng instance trước khi ghi thay đổi vào database. Nếu bước database lỗi sau đó, người dùng cần xin OTP mới. Gửi lại OTP thay dữ liệu chờ cũ.
- Đổi email có luồng OTP riêng trong `ProfileService`, cũng dùng bộ nhớ ứng dụng.
- OTP mất khi API khởi động lại. Không có dữ liệu OTP dùng chung giữa nhiều instance.

## Kiểm thử

Chạy từ thư mục gốc repository:

```powershell
dotnet restore SportsCenterAPI.slnx
dotnet build SportsCenterAPI.slnx --no-restore
dotnet test SportsCenterAPI.slnx --no-build --verbosity normal
```

Chỉ chạy project test:

```powershell
dotnet test tests/SportsCenterAPI.Tests/SportsCenterAPI.Tests.csproj
```

Các nhóm test hiện có gồm lớp học/đặt chỗ, đăng ký gói, điểm danh, hồ sơ và `MemoryOtpStore`. Test dùng EF Core InMemory, không chứng minh được SQL translation, transaction hoặc race condition của PostgreSQL thật. GitHub Actions chạy build/test khi push vào các nhánh được khai báo hoặc mở PR vào `master`.

Khi kiểm thử thủ công trên Swagger, kiểm tra cả thành công và ngoại lệ: thiếu/sai token, sai vai trò, truy cập dữ liệu người khác, OTP sai/hết hạn, đặt trùng/hết chỗ/trùng lịch, hủy quá hạn và các trạng thái thanh toán. Dùng database kiểm thử riêng và chỉ xóa dữ liệu do lượt test tạo ra. README không khẳng định toàn bộ testcase đã pass.

## Quy ước phát triển

- Ưu tiên xử lý HTTP ở controller, nghiệp vụ ở service và truy vấn dữ liệu qua `AppDbContext`.
- Dùng DTO cho dữ liệu trao đổi; lấy người thao tác từ JWT khi xử lý quyền sở hữu.
- Kiểm tra vai trò và quyền truy cập dữ liệu tại các thao tác liên quan.
- Dùng lỗi nghiệp vụ và middleware hiện có; giữ chú thích XML để Swagger hiển thị mô tả endpoint.
- Khi thay đổi schema, bổ sung migration và kiểm tra trước trên database riêng.
- Cập nhật `.env.example` khi thêm cấu hình; chỉ đưa giá trị mẫu vào Git.
- Chạy kiểm thử phù hợp trước khi tạo PR; không thử giao dịch thật hoặc ghi dữ liệu vào database dùng chung ngoài phạm vi được thống nhất.

## Giới hạn hiện tại

- OTP trong bộ nhớ phù hợp với một instance API; triển khai nhiều instance cần kho dùng chung.
- Lịch sử chat và câu trả lời AI chưa được lưu; AI không thay thế việc duyệt giáo án của HLV.
- Response chưa đồng nhất hoàn toàn; một số controller còn truy cập database trực tiếp.
- Unit test InMemory cần được bổ sung bằng kiểm thử PostgreSQL và kiểm thử tích hợp cho các luồng quan trọng.
- Swagger ngoài Development cần `OpenApi__Enabled=true`; CORS và URL frontend/payOS cần khớp môi trường triển khai.

## License

Repository hiện chưa có tệp `LICENSE`. Liên hệ chủ repository trước khi phân phối hoặc sử dụng lại ngoài phạm vi được cho phép.
