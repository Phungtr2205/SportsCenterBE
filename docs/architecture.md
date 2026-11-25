# Kiến trúc hệ thống — Sports Center Management System

Tài liệu mô tả backend `SportsCenterAPI`: các thành phần, cách chúng gọi nhau, dữ liệu lưu ở đâu và các luồng nghiệp vụ chính. Các sơ đồ viết bằng Mermaid, GitHub tự vẽ thành hình.

## 1. Bối cảnh hệ thống

```mermaid
flowchart LR
  subgraph Client[Người dùng]
    FE["🌐 Frontend web<br/>sportcenter-fe.onrender.com<br/>localhost:3000 khi dev"]
    SW["🧪 Swagger UI<br/>/swagger"]
  end

  subgraph Host[Render Web Service · Docker]
    API["SportsCenterAPI<br/>ASP.NET Core .NET 10<br/>REST /api/*"]
    MEM["Bộ nhớ trong<br/>MemoryOtpStore (IMemoryCache)"]
  end

  subgraph Cloud[Dịch vụ bên ngoài]
    DB[("🗄 Supabase PostgreSQL<br/>EF Core 10 + Npgsql")]
    ST["🖼 Supabase Storage<br/>ảnh gói, lớp, bộ môn, phiếu hỗ trợ"]
    PAY["💳 payOS<br/>link thanh toán VietQR + webhook có chữ ký"]
    MAIL["✉ SendGrid<br/>Mail Send API qua HTTPS"]
    AI["🤖 GroqCloud<br/>gợi ý bài tập, chat"]
    GG["🔑 Google OAuth<br/>đăng nhập Google"]
  end

  FE -->|HTTPS + JWT Bearer| API
  SW -->|HTTPS + JWT Bearer| API
  API --- MEM
  API -->|SQL, transaction| DB
  API -->|upload ảnh| ST
  API -->|tạo / tra cứu / hủy link| PAY
  PAY -->|POST /api/payments/payos/webhook| API
  API -->|gửi OTP, thông báo| MAIL
  API -->|chat completions| AI
  API -->|xác minh idToken / đổi code| GG
```

| Thành phần | Vai trò | Cấu hình |
|---|---|---|
| SportsCenterAPI | Toàn bộ nghiệp vụ, một ứng dụng duy nhất (monolith) | `Program.cs` |
| Supabase PostgreSQL | Lưu toàn bộ dữ liệu nghiệp vụ | `ConnectionStrings:DefaultConnection` |
| Supabase Storage | Lưu file ảnh, database chỉ lưu URL | `SupabaseStorage:*` |
| payOS | Thanh toán online bằng VietQR | `PayOS:*` |
| SendGrid | Gửi email OTP | `SendGrid:ApiKey`, `MailSettings:*` |
| GroqCloud | Mô hình AI | `AI:ApiKey`, `AI:Model` |
| Google OAuth | Đăng nhập bằng Google | `GoogleAuth:*` |
| MemoryOtpStore | Lưu OTP đăng ký / quên mật khẩu, hết hạn 5 phút | Không cần cấu hình |

## 2. Kiến trúc phân tầng

```mermaid
flowchart TB
  REQ([HTTP request]) --> MW

  subgraph Pipeline[ASP.NET Core pipeline]
    MW["ExceptionMiddleware<br/>bắt mọi exception → JSON lỗi"]
    CORS["CORS AllowFrontend"]
    AUTHN["JwtBearer<br/>kiểm tra chữ ký, hạn token<br/>OnTokenValidated: tài khoản còn hoạt động, role chưa đổi"]
    AUTHZ["Authorization<br/>[Authorize(Roles = ...)]"]
    MW --> CORS --> AUTHN --> AUTHZ
  end

  AUTHZ --> C

  subgraph App[SportsCenterAPI]
    C["Controllers (23)<br/>nhận request, lấy ActorId từ JWT, trả ApiResponse"]
    S["Services (19)<br/>nghiệp vụ, kiểm tra quyền sở hữu, transaction"]
    H["Helpers<br/>JwtHelper, PasswordHelper, PackageTypeHelper,<br/>BusinessException, PaginationHelper"]
    D["AppDbContext<br/>EF Core, index, ràng buộc"]
    C --> S
    S --> H
    S --> D
  end

  D --> DB[(PostgreSQL)]
  S --> EXT[payOS · SendGrid · GroqCloud · Supabase Storage · Google]
```

**Quy ước giữa các tầng**
- **Controller** không chứa nghiệp vụ. Chỉ lấy `ActorId` (id người đang đăng nhập) từ claim `NameIdentifier` trong JWT, gọi service, rồi bọc kết quả trong `ApiResponse`.
- **Service** tự kiểm tra lại quyền sở hữu dữ liệu: Member chỉ thao tác trên dữ liệu của mình. Thao tác ghi quan trọng (thanh toán, đặt chỗ, đăng ký gói) chạy trong transaction `Serializable`.
- **Lỗi nghiệp vụ** ném `BusinessException(statusCode, message)`; `ExceptionMiddleware` chuyển thành JSON. Xem [exception-matrix.md](exception-matrix.md).
- **Ngoại lệ đã biết:** `PaymentsController.ExportInvoicePdf` và `AuditLogsController` truy cập `AppDbContext` trực tiếp, không qua service.

**Đăng ký service (`Program.cs`)**
- `Scoped`: các service nghiệp vụ, mỗi request một instance, dùng chung `AppDbContext` của request đó.
- `Singleton`: `MemoryOtpStore`, `TimeProvider`.
- `HttpClient`: `EmailService` (timeout 30 giây), `AIService` (60 giây), `SupabaseImageStorage`, `GoogleTokenVerifier`.

## 3. Mô hình dữ liệu

25 entity trong `Models/`. Sơ đồ dưới chỉ vẽ quan hệ chính.

```mermaid
erDiagram
  User ||--o| Member : "là"
  User ||--o| Coach : "là"
  User ||--o{ RefreshToken : "có phiên"
  User ||--o{ Notification : "nhận"
  User ||--o{ AuditLog : "thực hiện"
  User ||--o{ AIChatHistory : "chat"
  User ||--o{ AIWorkoutHistory : "xin gợi ý"

  MembershipPackage ||--o{ MemberSubscription : "được đăng ký"
  Member ||--o{ MemberSubscription : "đăng ký"
  MemberSubscription ||--o{ Payment : "được thanh toán"
  Member ||--o{ Payment : "trả"

  Sport ||--o{ SportClass : "có lớp"
  Coach ||--o{ SportClass : "phụ trách"
  SportClass ||--o{ ClassSession : "có buổi"
  Coach ||--o{ ClassSession : "dạy"
  Room ||--o{ ClassSession : "diễn ra tại"
  CancellationPolicy ||--o{ ClassSession : "áp dụng"
  ClassSession ||--o{ ClassRegistration : "được đặt"
  Member ||--o{ ClassRegistration : "đặt chỗ"
  ClassRegistration ||--o| ClassReview : "được đánh giá"
  ClassSession ||--o{ Attendance : "điểm danh"
  Member ||--o{ Attendance : "có mặt"

  Coach ||--o{ CoachReview : "được đánh giá"
  Member ||--o{ CoachReview : "đánh giá"

  Member ||--o{ TrainingPlan : "theo"
  Coach ||--o{ TrainingPlan : "soạn"
  TrainingPlan ||--o{ TrainingPlanExercise : "gồm"
  Exercise ||--o{ TrainingPlanExercise : "được dùng"

  Member ||--o{ GymCheckIn : "ra vào"
  MemberSubscription ||--o{ GymCheckIn : "dùng gói"
  Member ||--o{ SupportTicket : "gửi"

  User {
    int Id PK
    string Email UK
    string PasswordHash
    string Role
    bool IsActive
  }
  MemberSubscription {
    int Id PK
    int MemberId FK
    int PackageId FK
    decimal AgreedPrice
    int DurationInDays
    string Status
    datetime StartDate
    datetime EndDate
  }
  Payment {
    int Id PK
    int SubscriptionId FK
    decimal Amount
    decimal AmountReceived
    string PaymentMethod
    string Status
    long PayOSOrderCode UK
    string TransactionReference UK
    string MemberNameSnapshot
    string PackageNameSnapshot
  }
  ClassSession {
    int Id PK
    int ClassId FK
    int CoachId FK
    int RoomId FK
    datetime StartsAt
    datetime EndsAt
    int Capacity
    string Status
  }
  ClassRegistration {
    int Id PK
    int MemberId FK
    int SessionId FK
    string Status
    datetime CancellationDeadline
  }
```

**Ràng buộc chống trùng ở database** (khai báo trong `Data/AppDbContext.cs`):

| Index unique | Chặn tình huống |
|---|---|
| `Users.Email` | Hai tài khoản cùng email |
| `Members.UserId`, `Coaches.UserId` | Một tài khoản có hai hồ sơ hội viên / huấn luyện viên |
| `ClassRegistrations(MemberId, SessionId)` khi chưa hủy | Đặt cùng một buổi hai lần |
| `Attendances(MemberId, SessionId)` | Điểm danh hai lần |
| `ClassReviews.RegistrationId` | Đánh giá một lượt học hai lần |
| `Payments.SubscriptionId` khi `Status = 'Completed'` | Một đăng ký gói được thanh toán hai lần |
| `Payments.PayOSOrderCode` | Hai giao dịch dùng chung mã đơn payOS |
| `Payments.TransactionReference` | Trùng mã biên lai |

**Snapshot lúc mua:** `MemberSubscription.AgreedPrice` và `DurationInDays` chốt giá và thời hạn lúc đăng ký, nên Manager đổi giá gói sau đó không ảnh hưởng khách đã mua. `Payment.MemberNameSnapshot` và `PackageNameSnapshot` giữ tên trên biên lai.

**Thời gian:** database lưu UTC. Biên lai PDF hiển thị UTC+7.

**Schema:** quản lý bằng EF Core migrations trong `Migrations/`. Khi `Database:InitializeOnStartup = true`, app tự chạy migration lúc khởi động; dữ liệu mẫu (`DbSeeder`) chỉ nạp ở môi trường Development.

## 4. Luồng đăng nhập và làm mới token

```mermaid
sequenceDiagram
  autonumber
  actor U as Người dùng
  participant FE as Frontend
  participant API as AuthController / AuthService
  participant DB as PostgreSQL

  U->>FE: Nhập email, mật khẩu
  FE->>API: POST /api/Auth/login
  API->>DB: Tìm User theo email (đã chuẩn hóa)
  API->>API: BCrypt kiểm tra mật khẩu
  alt Sai email hoặc mật khẩu
    API-->>FE: 401 Invalid email or password.
  else Đúng
    API->>DB: Lưu RefreshToken (chỉ lưu SHA-256 của token)
    API-->>FE: accessToken (JWT ngắn hạn) + refreshTokenKey
  end

  Note over FE,API: Gọi API khác với header Authorization: Bearer accessToken
  FE->>API: GET /api/... 
  API->>DB: OnTokenValidated: tài khoản còn IsActive, role chưa đổi?
  alt Bị khóa hoặc đổi role
    API-->>FE: 401
  end

  Note over FE,API: Access token hết hạn
  FE->>API: POST /api/Auth/refresh-token
  API->>DB: Tìm theo hash token hiện tại, hoặc hash token cũ xoay trong 30 giây
  API->>DB: UPDATE nguyên tử: TokenHash = hash mới, PreviousTokenHash = hash cũ
  alt Request khác đã xoay trước
    API-->>FE: 401 Refresh token has already been used or revoked.
  else Thành công
    API-->>FE: accessToken mới + refreshTokenKey mới (ExpiresAt giữ nguyên)
  end
```

- Refresh token **không kéo dài** thời hạn phiên: `ExpiresAt` giữ nguyên qua mỗi lần xoay.
- Token cũ được chấp nhận lại trong 30 giây (`RefreshGrace`), phòng trường hợp phản hồi refresh bị mất trên đường truyền làm người dùng mất phiên.
- Đăng ký tài khoản: `send-register-otp` → OTP gửi qua SendGrid, lưu hash trong `MemoryOtpStore` 5 phút, sai tối đa 5 lần → `verify-register-otp` tạo User + Member và đăng nhập luôn.

## 5. Luồng đăng ký gói và thanh toán payOS

```mermaid
sequenceDiagram
  autonumber
  actor M as Hội viên
  participant FE as Frontend
  participant SUB as SubscriptionService
  participant PS as PaymentService
  participant DB as PostgreSQL
  participant P as payOS

  M->>FE: Chọn gói
  FE->>SUB: POST /api/member-subscriptions
  SUB->>DB: Tạo MemberSubscription Pending, chốt AgreedPrice, DurationInDays
  SUB-->>FE: subscriptionId

  FE->>PS: POST /api/payments { subscriptionId }
  PS->>DB: Transaction Serializable: kiểm tra gói Pending, chưa có Completed / NeedsReview
  alt Đã có Payment Pending
    PS->>DB: Dùng lại Payment cũ (cùng orderCode)
  else Chưa có
    PS->>DB: nextval(PayOSOrderNumbers), tạo Payment Pending
  end
  PS->>P: Tạo link thanh toán (orderCode, amount)
  PS->>P: Tra cứu lại link
  PS-->>FE: checkoutUrl, qrCode

  M->>P: Quét VietQR, chuyển khoản
  P->>PS: POST /api/payments/payos/webhook (có chữ ký)
  PS->>PS: Xác minh chữ ký, code = 00, VND
  alt Payment đã Completed
    PS-->>P: 200 (bỏ qua, chống xử lý lặp)
  else
    PS->>P: Tra cứu trạng thái thật (không tin số liệu webhook)
    PS->>DB: Transaction Serializable
    alt PAID, đủ tiền, gói còn Pending, không có giao dịch khác
      PS->>DB: Payment Completed, mã biên lai INV-...
      PS->>DB: Subscription Active (StartDate, EndDate), AuditLog, Notification
    else Có tiền nhưng sai điều kiện
      PS->>DB: Payment NeedsReview + ReviewReason
    end
    PS-->>P: 200
  end

  M->>FE: Quay lại trang ReturnUrl
  FE->>PS: POST /api/payments/{id}/sync
  PS-->>FE: Trạng thái mới nhất
```

- **Nâng cấp gói Standard:** nếu hội viên đang có gói Standard còn hạn, gói mới kế thừa `StartDate` của gói cũ và gói cũ chuyển `Upgraded`.
- **Thu tiền tại quầy:** lễ tân gọi `POST /api/member-subscriptions/{id}/payments`. Bị chặn nếu còn link payOS `Pending` hoặc giao dịch `NeedsReview`, để không thu tiền hai lần.
- Chi tiết trạng thái và các tình huống chống trùng: [api/payments.md](api/payments.md).

## 6. Luồng đặt chỗ buổi học và điểm danh

```mermaid
sequenceDiagram
  autonumber
  actor M as Hội viên
  actor C as Huấn luyện viên
  participant API as ClassService / AttendanceService
  participant DB as PostgreSQL

  M->>API: GET /api/class-sessions (tìm buổi)
  M->>API: POST /api/class-sessions/{id}/registrations
  API->>DB: Kiểm tra: gói còn hạn, buổi còn nhận đăng ký,<br/>còn chỗ, chưa đặt, không trùng giờ buổi khác
  API->>DB: Tạo ClassRegistration Registered + CancellationDeadline theo chính sách hủy
  API-->>M: Đặt chỗ thành công

  opt Hủy trước hạn
    M->>API: POST /api/class-registrations/{id}/cancel
    API->>DB: Kiểm tra chưa quá CancellationDeadline, chưa điểm danh
    API->>DB: Registration Cancelled, trả lại chỗ
  end

  Note over C,API: Từ 30 phút trước giờ học
  C->>API: GET /api/class-sessions/{id}/roster
  C->>API: POST /api/Attendances/mark (Present / Late / Absent / Excused)
  API->>DB: Lưu Attendance (unique MemberId + SessionId)

  C->>API: POST /api/class-sessions/{id}/complete
  API->>DB: Session Completed, các Registration Completed

  M->>API: POST /api/class-registrations/{id}/review
  API->>DB: Chỉ khi buổi Completed và có điểm danh Present / Late, mỗi lượt một lần
```

## 7. Luồng check-in vào trung tâm

```mermaid
sequenceDiagram
  autonumber
  actor M as Hội viên
  actor R as Lễ tân
  participant API as GymCheckInService
  participant DB as PostgreSQL

  M->>API: GET /api/GymCheckIns/my-qr
  API-->>M: QrData = SC-MEM-000123, tên gói, ngày hết hạn
  M->>R: Đưa mã QR
  R->>API: POST /api/GymCheckIns/scan-qr { qrCode }
  API->>API: Tách mã hội viên (SC-MEM-..., SC-..., số id) hoặc coi là số điện thoại
  API->>DB: Có gói Active còn hạn?
  alt Không có gói
    API-->>R: 400 Hội viên không có gói tập nào đang hoạt động...
  else Đang ở trong trung tâm (chưa check-out)
    API-->>R: 400 Hội viên này đã check-in trước đó và chưa check-out!
  else
    API->>DB: Tạo GymCheckIn (CheckInTime, gói đang dùng, nhân viên quét)
    API-->>R: Thông tin hội viên và gói
  end
  R->>API: PUT /api/GymCheckIns/{id}/check-out
  API->>DB: Ghi CheckOutTime, tính thời gian ở lại
```

## 8. Bảo mật

| Cơ chế | Cách làm |
|---|---|
| Mật khẩu | BCrypt (`PasswordHelper`), tối đa 72 byte |
| Access token | JWT HMAC, hạn `Jwt:ExpireMinutes` (mặc định 15 phút), chứa `NameIdentifier`, `Role` |
| Refresh token | Chuỗi ngẫu nhiên 64 byte, database chỉ lưu SHA-256, xoay mỗi lần dùng |
| Khóa tài khoản / đổi role | Kiểm tra lại mỗi request trong `OnTokenValidated`, token cũ hết hiệu lực ngay |
| OTP | 6 chữ số ngẫu nhiên (`RandomNumberGenerator`), lưu SHA-256, hết hạn 5 phút, sai tối đa 5 lần |
| Quyền dữ liệu | Service so `ActorId` với chủ sở hữu bản ghi |
| Webhook payOS | Xác minh chữ ký bằng `PayOS:ChecksumKey`, sau đó tra cứu lại payOS |
| CORS | Chỉ cho `http://localhost:3000` và `https://sportcenter-fe.onrender.com` |
| Dữ liệu mẫu | `DbSeeder` chỉ chạy ở Development vì mật khẩu mẫu công khai trong repo |
| Lỗi 500 | Không trả stack trace hay SQL cho client |

## 9. Giới hạn kiến trúc hiện tại

- **Một instance:** OTP nằm trong bộ nhớ app, mất khi restart và không dùng chung được giữa nhiều instance.
- **Chưa có job nền:** gói hết hạn được xác định khi truy vấn (`EndDate`), không có job tự đổi trạng thái.
- **Chưa giới hạn số lần gọi** (rate limit) cho API OTP và đăng nhập.
- **Test** dùng EF Core InMemory, không kiểm được transaction `Serializable` và index có điều kiện của PostgreSQL thật.
- **Loại gói** (Standard / PT) có chỗ vẫn đoán theo tên và mô tả gói qua `PackageTypeHelper`.
