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
          +----> SMTP: gửi email và OTP
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
| Email | MailKit, MimeKit, SMTP STARTTLS |
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
├── Settings/                    # Cấu hình SMTP, payOS
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
- SMTP để kiểm thử gửi OTP; thông tin payOS để kiểm thử thanh toán online; API key GroqCloud để dùng AI.

| Cách chạy | Địa chỉ Swagger |
|---|---|
| Profile `https` | `https://localhost:7182/swagger` |
| Profile `http` | `http://localhost:5212/swagger` |
| Docker Compose | `http://localhost:8081/swagger` |
