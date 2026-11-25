# Hướng dẫn cấu hình và deploy

Phần chạy local (User Secrets, `dotnet run`, Docker Compose) đã có trong [README](../README.md#khởi-chạy-local). Tài liệu này bổ sung:
- deploy lên Render bằng Docker
- nối các dịch vụ bên ngoài: Supabase, payOS, SendGrid, GroqCloud, Google
- cập nhật database khi có migration mới
- checklist trước buổi demo

Thay `<api-domain>` bằng domain thật của service backend trên Render, ví dụ `ten-service.onrender.com`.

## 1. Tổng quan môi trường

| Môi trường | Chạy ở đâu | Database | Seed dữ liệu mẫu | Swagger |
|---|---|---|---|---|
| Development | Máy cá nhân (`dotnet run`) | PostgreSQL local hoặc Supabase riêng | Có, nếu `InitializeOnStartup = true` | Luôn bật |
| Docker local | `docker compose up` | Theo `.env` | Không (chạy `Production`) | Bật (`OpenApi__Enabled=true` trong `compose.yaml`) |
| Production | Render Web Service | Supabase | Không bao giờ | Chỉ khi `OpenApi__Enabled=true` |

Dữ liệu mẫu chỉ nạp ở `Development` vì mật khẩu mẫu (`Password@123`) công khai trong repo.

## 2. Biến môi trường đầy đủ

ASP.NET Core đọc `:` trong JSON và `__` trong biến môi trường: `Jwt:SecretKey` ↔ `Jwt__SecretKey`.

| Biến | Bắt buộc | Ví dụ / ghi chú |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Có | `Production` (Dockerfile đã đặt sẵn) |
| `PORT` | Không | Render tự cấp; Dockerfile mặc định `8080` |
| `ConnectionStrings__DefaultConnection` | Có | `Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require` |
| `Database__InitializeOnStartup` | Không | Mặc định `true` trong code. `true` = tự chạy migration lúc khởi động |
| `Jwt__SecretKey` | Có | Chuỗi ngẫu nhiên ≥ 32 byte |
| `Jwt__Issuer` | Có | `SportsCenterAPI` |
| `Jwt__Audience` | Có | `SportsCenterClient` |
| `Jwt__ExpireMinutes` | Không | `15` |
| `Jwt__RefreshTokenExpireDays` | Không | `7` |
| `SendGrid__ApiKey` | Có (để gửi OTP) | API key quyền Mail Send |
| `MailSettings__SenderName` | Có | `Sports Center` |
| `MailSettings__SenderEmail` | Có | Email đã xác minh trên SendGrid |
| `PayOS__ClientId`, `PayOS__ApiKey`, `PayOS__ChecksumKey` | Có (để thanh toán online) | Lấy trên trang payOS |
| `PayOS__ReturnUrl` | Có | `https://sportcenter-fe.onrender.com/payment/success` |
| `PayOS__CancelUrl` | Có | `https://sportcenter-fe.onrender.com/payment/cancel` |
| `AI__ApiKey` | Có (để dùng AI) | Key GroqCloud |
| `AI__Model` | Có | Ví dụ `openai/gpt-oss-20b` |
| `SupabaseStorage__Url` | Có (để upload ảnh) | `https://<project>.supabase.co` |
| `SupabaseStorage__ServiceKey` | Có (để upload ảnh) | Service role key của Supabase. **Không** đưa cho frontend |
| `SupabaseStorage__Bucket` | Có (để upload ảnh) | Tên bucket public chứa ảnh |
| `GoogleAuth__ClientId` | Không | Chỉ cần nếu dùng đăng nhập Google |
| `GoogleAuth__ClientSecret` | Không | Chỉ cần khi frontend gửi `code` thay cho `idToken` |
| `OpenApi__Enabled` | Không | `true` để bật Swagger trên Production |

Thiếu biến của dịch vụ nào thì chỉ chức năng đó lỗi (`503` hoặc `500`), app vẫn chạy. Xem [exception-matrix.md](exception-matrix.md).

`.env.example` hiện chưa có các biến `SupabaseStorage__*` và `GoogleAuth__*`. Khi thêm biến mới, cập nhật file này theo quy ước trong README.

## 3. Deploy lên Render

### 3.1 Tạo service

1. Render Dashboard → **New → Web Service** → chọn repo GitHub `SportsCenterBE`, nhánh `master`.
2. **Runtime:** Docker. Render tự dùng `Dockerfile` ở thư mục gốc.
3. **Environment Variables:** nhập toàn bộ biến ở mục 2. Không cần đặt `PORT`.
4. Bấm **Deploy**.

Dockerfile build qua 2 giai đoạn:
- Image `sdk:10.0` để `dotnet publish`.
- Image `aspnet:10.0` để chạy, có thêm `libfontconfig1` để QuestPDF tạo biên lai PDF.

App chạy bằng user thường (không phải root) và lắng nghe `0.0.0.0:${PORT}`.

### 3.2 Kiểm tra sau deploy

| Kiểm tra | Cách làm | Mong đợi |
|---|---|---|
| App sống | Mở `https://<api-domain>/` | Chuyển sang `/swagger` (nếu bật) |
| Database | `GET /api/membership-packages` | `200` |
| Đăng nhập | `POST /api/Auth/login` | `200` |
| Email | `POST /api/Auth/send-register-otp` với email thật | Nhận được mã |
| Log | Render → Logs | Không có lỗi kết nối database |

### 3.3 Lưu ý với gói Free của Render

- Service ngủ sau một thời gian không có request, lần gọi đầu sau đó chậm vài chục giây. Mở API trước buổi demo khoảng 1–2 phút.
- Render Free chặn cổng SMTP, nên email gửi qua SendGrid HTTPS API (cổng 443).
- Restart làm mất OTP đang chờ, vì OTP lưu trong bộ nhớ app.

## 4. Supabase

### 4.1 Database

1. Supabase → **Project Settings → Database → Connection string**. Lấy host, port, user, password.
2. Ghép thành chuỗi Npgsql: `Host=...;Port=...;Database=postgres;Username=...;Password=...;SSL Mode=Require`. Không dùng nguyên URI `postgresql://...`.

### 4.2 Storage (ảnh)

1. Supabase → **Storage → New bucket**, bật **Public** để frontend hiển thị ảnh qua URL.
2. Lấy `service_role` key ở **Project Settings → API** cho `SupabaseStorage__ServiceKey`.
3. API chấp nhận `.jpg`, `.png`, `.webp`, tối đa 2MB.

## 5. Cập nhật database khi có migration mới

Migration nằm trong `Migrations/`. Có 2 cách:

**Cách 1: tự động khi khởi động** (`Database__InitializeOnStartup=true`)
- App gọi `Database.Migrate()` mỗi lần khởi động và áp các migration còn thiếu.
- Đơn giản, nhưng **mọi máy trỏ vào cùng database** (kể cả máy cá nhân của thành viên) đều có thể áp migration lên database chung.

**Cách 2: chạy tay** (`Database__InitializeOnStartup=false`)

```powershell
dotnet tool restore
```

```powershell
dotnet ef database update --project SportsCenterAPI.csproj --connection "<chuỗi kết nối>"
```

**Quy tắc cho nhóm**
- Backup database Supabase trước khi áp migration lên database demo.
- Chỉ một người tạo migration tại một thời điểm, để tránh xung đột `AppDbContextModelSnapshot.cs`.
- Pull code mới nhất trước khi chạy `dotnet ef migrations add`.

## 6. payOS

1. Đăng ký trên [my.payos.vn](https://my.payos.vn), tạo kênh thanh toán, lấy `Client ID`, `API Key`, `Checksum Key`.
2. **Cấu hình webhook:** trong kênh thanh toán, đặt Webhook URL là:

   ```text
   https://<api-domain>/api/payments/payos/webhook
   ```

   Khi lưu, payOS gửi một webhook mẫu. API trả `200` và bỏ qua vì không có đơn tương ứng.
3. `PayOS__ReturnUrl` và `PayOS__CancelUrl` là trang **frontend**. Sau khi khách quay về, frontend gọi `POST /api/payments/{id}/sync` để lấy trạng thái mới nhất.
4. Webhook phải gọi được từ Internet. Khi chạy local, webhook không tới máy bạn được; dùng `sync` để cập nhật trạng thái.

## 7. SendGrid, GroqCloud, Google

- **SendGrid:** xem các bước chi tiết trong [README → Thiết lập SendGrid](../README.md#thiết-lập-sendgrid).
- **GroqCloud:** tạo key tại `console.groq.com/keys`, đặt `AI__ApiKey` và `AI__Model`. Hết hạn mức thì API AI trả `503`.
- **Google:**
  1. Google Cloud Console → **APIs & Services → Credentials** → tạo OAuth Client ID loại Web.
  2. Thêm domain frontend vào Authorized JavaScript origins (và redirect URI nếu dùng `code`).
  3. Đặt `GoogleAuth__ClientId` (và `GoogleAuth__ClientSecret` nếu dùng `code`).

## 8. CORS

Domain frontend được phép gọi API khai báo cứng trong `Program.cs`:

```csharp
policy.WithOrigins(
    "http://localhost:3000",
    "https://sportcenter-fe.onrender.com")
```

Đổi domain frontend thì phải sửa ở đây rồi deploy lại backend. Nếu thiếu, trình duyệt báo lỗi CORS dù API vẫn chạy bình thường.

## 9. CI

`ci.yml` (GitHub Actions) chạy `restore` → `build` → `test` trên .NET 10 khi:
- push vào `master`, `unittest*`, `nguyen*`
- mở pull request vào `master`

Không merge PR khi CI đỏ.

## 10. Checklist trước buổi demo

| # | Việc | Xong |
|---|---|---|
| 1 | Pull `master` mới nhất, `dotnet test` đạt hết | |
| 2 | Backup database Supabase | |
| 3 | Migration mới đã áp lên database demo | |
| 4 | Có sẵn tài khoản cho cả 4 role (Manager phải tạo tay, seed không tạo) | |
| 5 | Có dữ liệu: gói, phòng, lớp, buổi học sắp tới, một buổi đã hoàn thành có điểm danh và đánh giá | |
| 6 | Webhook payOS trỏ đúng URL backend đang chạy | |
| 7 | Thử OTP: email tới được (kiểm tra cả thư mục spam) | |
| 8 | Thử AI một câu, còn hạn mức GroqCloud | |
| 9 | Mở backend trước 1–2 phút để Render "thức dậy" | |
| 10 | Mỗi người trong nhóm đăng nhập lại một lần (token cũ có thể không còn hợp lệ sau khi đổi cơ chế phiên) | |
| 11 | Chuẩn bị kịch bản theo [test-plan.md](test-plan.md): J01 → J02 → J05 → J06 → J07 | |
