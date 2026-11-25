# Danh sách endpoint API (`/api/*`)

Danh sách **toàn bộ** HTTP endpoint của backend, lấy trực tiếp từ các file trong `Controllers/`.

Tổng: **131** endpoint trong **23** controller.

Swagger (`/swagger`) vẫn là nơi xem đầy đủ DTO và tham số. File này dùng để tra nhanh: endpoint nào, ai được gọi, thuộc controller nào.

Chi tiết từng nhóm:
- Thanh toán: [api/payments.md](api/payments.md)

## Quy ước

**Cột Quyền**
- `Public`: không cần đăng nhập (`[AllowAnonymous]`).
- `Đăng nhập`: cần JWT, role nào cũng được.
- Tên role: chỉ các role đó được gọi. Role trong code: `Member`, `Receptionist`, `Coach`, `Manager`.
- `FrontDesk` trong code là nhóm `Manager,Receptionist`, không phải role riêng.

**Xác thực:** gửi header `Authorization: Bearer <accessToken>`. Access token lấy từ `/api/Auth/login`, `/api/Auth/google` hoặc `/api/Auth/verify-register-otp`.

**Response thành công:** phần lớn bọc trong `ApiResponse`:

```json
{ "success": true, "statusCode": 200, "message": "...", "data": { }, "errors": null, "timestamp": "..." }
```

**Response lỗi:** do `ExceptionMiddleware` trả về:

```json
{ "statusCode": 409, "message": "..." }
```

Xem bảng lỗi chung ở [exception-matrix.md](exception-matrix.md).

**Thời gian:** gửi theo ISO 8601 có múi giờ, ví dụ `2026-11-10T19:00:00+07:00`. Server lưu và trả về UTC (`...Z`).

**Đường dẫn:** route không phân biệt hoa thường. Các controller dùng `[controller]` có tên viết hoa (ví dụ `/api/Auth`); bảng dưới giữ đúng tên đó.

## Mục lục

| Controller | Prefix | Số endpoint |
|---|---|---|
| [AuthController](#auth--apiauth) | `/api/Auth` | 8 |
| [ProfileController](#profile--apiprofile) | `/api/profile` | 4 |
| [UsersController](#users--apiusersstaff) | `/api/users/staff` | 5 |
| [MembersController](#members--apimembers) | `/api/members` | 5 |
| [MembershipPackagesController](#membership-packages--apimembership-packages) | `/api/membership-packages` | 8 |
| [MemberSubscriptionsController](#member-subscriptions--apimember-subscriptions) | `/api/member-subscriptions` | 8 |
| [PaymentsController](#payments--apipayments) | `/api/payments` | 7 |
| [ClassCatalogController](#sports-và-coaches--api) | `/api` | 6 |
| [CoachReviewsController](#coach-reviews--apicoaches) | `/api/coaches` | 4 |
| [SportClassesController](#classes--apiclasses) | `/api/classes` | 10 |
| [ClassSessionsController](#class-sessions--apiclass-sessions) | `/api/class-sessions` | 8 |
| [ClassRegistrationsController](#class-registrations--apiclass-registrations) | `/api/class-registrations` | 5 |
| [CancellationPoliciesController](#cancellation-policies--apicancellation-policies) | `/api/cancellation-policies` | 3 |
| [AttendancesController](#attendances--apiattendances) | `/api/Attendances` | 3 |
| [RoomsController](#rooms--apirooms) | `/api/Rooms` | 6 |
| [GymCheckInsController](#gym-check-ins--apigymcheckins) | `/api/GymCheckIns` | 8 |
| [ExercisesController](#exercises--apiexercises) | `/api/Exercises` | 6 |
| [TrainingPlansController](#training-plans--apitrainingplans) | `/api/TrainingPlans` | 9 |
| [AIController](#ai--apiai) | `/api/ai` | 7 |
| [NotificationsController](#notifications--apinotifications) | `/api/notifications` | 2 |
| [SupportTicketsController](#support-tickets--apisupporttickets) | `/api/SupportTickets` | 5 |
| [ReportsController](#reports--apireports) | `/api/Reports` | 3 |
| [AuditLogsController](#audit-logs--apiaudit-logs) | `/api/audit-logs` | 1 |
| **Tổng** | | **131** |

## Quyền theo role (tóm tắt)

| Nhóm chức năng | Member | Receptionist | Coach | Manager |
|---|---|---|---|---|
| Đăng nhập, hồ sơ, thông báo | ✓ | ✓ | ✓ | ✓ |
| Đăng ký gói, thanh toán online | ✓ (của mình) | ✓ (thay hội viên) | | ✓ (thay hội viên) |
| Xác nhận thu tiền tại quầy | | ✓ | | ✓ |
| Đặt / hủy buổi học | ✓ (của mình) | ✓ (thay hội viên) | | ✓ (thay hội viên) |
| Điểm danh buổi học | ✓ (tự check-in) | ✓ | ✓ | ✓ |
| Check-in vào trung tâm | Lấy mã QR | ✓ | | ✓ |
| Giáo án | Xem, xin đổi | Xem | Tạo, sửa, xóa | |
| Danh mục: gói, lớp, buổi, phòng, bộ môn | Xem | Xem | Xem | Quản lý |
| Nhân viên, báo cáo, audit log | | | | ✓ |
| AI gợi ý bài tập | ✓ | | ✓ | |
| AI chat | ✓ | | | |

---

## Auth — `/api/Auth`

`AuthController`. Không yêu cầu đăng nhập.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/Auth/login` | Public | Đăng nhập bằng email và mật khẩu, nhận access token và refresh token |
| POST | `/api/Auth/google` | Public | Đăng nhập bằng Google (`idToken`, hoặc `code` + `redirectUri`) |
| POST | `/api/Auth/send-register-otp` | Public | Gửi OTP xác nhận đăng ký qua email |
| POST | `/api/Auth/verify-register-otp` | Public | Xác thực OTP, tạo tài khoản hội viên và đăng nhập luôn |
| POST | `/api/Auth/request-reset-password` | Public | Gửi OTP đặt lại mật khẩu |
| POST | `/api/Auth/verify-reset-password` | Public | Xác thực OTP và đặt mật khẩu mới |
| POST | `/api/Auth/refresh-token` | Public | Đổi refresh token lấy cặp token mới |
| POST | `/api/Auth/logout` | Public | Thu hồi refresh token |

Ghi chú:
- OTP có hiệu lực 5 phút, sai tối đa 5 lần.
- Refresh token được xoay vòng mỗi lần dùng. Token cũ vẫn được chấp nhận lại trong 30 giây, phòng trường hợp phản hồi bị mất trên đường truyền.

## Profile — `/api/profile`

`ProfileController`. Mọi role đã đăng nhập.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/profile` | Đăng nhập | Xem thông tin tài khoản đang đăng nhập |
| PUT | `/api/profile` | Đăng nhập | Cập nhật thông tin hoặc đổi mật khẩu |
| POST | `/api/profile/change-email/request-otp` | Đăng nhập | Gửi OTP tới email mới |
| POST | `/api/profile/change-email/verify-otp` | Đăng nhập | Xác thực OTP và đổi email |

## Users — `/api/users/staff`

`UsersController`. Chỉ `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/users/staff` | Manager | Danh sách tài khoản nhân viên |
| GET | `/api/users/staff/{id}` | Manager | Chi tiết tài khoản nhân viên |
| POST | `/api/users/staff` | Manager | Tạo tài khoản lễ tân, huấn luyện viên hoặc quản lý |
| PUT | `/api/users/staff/{id}/role` | Manager | Đổi vai trò nhân viên |
| DELETE | `/api/users/staff/{id}` | Manager | Vô hiệu hóa tài khoản nhân viên |

## Members — `/api/members`

`MembersController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/members` | Manager, Receptionist | Lễ tân tạo tài khoản hội viên tại quầy |
| GET | `/api/members` | Manager, Receptionist | Danh sách hội viên |
| GET | `/api/members/{id}` | Manager, Receptionist | Chi tiết một hội viên |
| GET | `/api/members/me` | Member | Hồ sơ hội viên của tôi |
| DELETE | `/api/members/{id}` | Manager | Vô hiệu hóa tài khoản hội viên |

## Membership Packages — `/api/membership-packages`

`MembershipPackagesController`. Mặc định `Manager`, trừ 2 API xem công khai.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/membership-packages` | Public | Danh sách gói đang bán |
| GET | `/api/membership-packages/{id}` | Public | Chi tiết gói đang bán |
| GET | `/api/membership-packages/manage` | Manager | Toàn bộ gói, kể cả gói đã ngừng |
| GET | `/api/membership-packages/manage/{id}` | Manager | Chi tiết một gói (quản lý) |
| POST | `/api/membership-packages` | Manager | Tạo gói |
| PUT | `/api/membership-packages/{id}` | Manager | Cập nhật gói |
| POST | `/api/membership-packages/{id}/image` | Manager | Upload ảnh gói (jpg/png/webp, tối đa 2MB) |
| DELETE | `/api/membership-packages/{id}` | Manager | Ngừng bán gói |

Đổi giá gói **không** ảnh hưởng đăng ký cũ: giá được chốt vào `MemberSubscription.AgreedPrice` lúc khách đăng ký.

## Member Subscriptions — `/api/member-subscriptions`

`MemberSubscriptionsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/member-subscriptions` | Member | Đăng ký gói, trạng thái `Pending` chờ thanh toán |
| POST | `/api/member-subscriptions/members/{memberId}` | Receptionist, Manager | Đăng ký gói thay cho hội viên |
| GET | `/api/member-subscriptions/mine` | Member | Các gói tôi đã đăng ký và thời hạn |
| GET | `/api/member-subscriptions/members/{memberId}` | Receptionist, Manager | Các gói của một hội viên |
| GET | `/api/member-subscriptions/{id}` | Member, Receptionist, Manager | Chi tiết một đăng ký gói |
| POST | `/api/member-subscriptions/{id}/cancel` | Member, Receptionist, Manager | Hủy đăng ký gói chưa thanh toán |
| POST | `/api/member-subscriptions/{id}/payments` | Receptionist, Manager | Xác nhận thu tiền tại quầy (Cash, BankTransfer, CreditCard) và kích hoạt gói |
| GET | `/api/member-subscriptions/upgrade-preview` | Member, Receptionist, Manager | Xem trước số tiền chênh lệch khi nâng cấp gói |

## Payments — `/api/payments`

`PaymentsController`. Chi tiết: [api/payments.md](api/payments.md).

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/payments` | Member, Receptionist, Manager | Lịch sử thanh toán (Member chỉ thấy của mình) |
| GET | `/api/payments/{id}` | Member, Receptionist, Manager | Chi tiết giao dịch |
| POST | `/api/payments` | Member, Receptionist, Manager | Tạo hoặc lấy lại link payOS đang chờ |
| POST | `/api/payments/{id}/sync` | Member, Receptionist, Manager | Hỏi lại payOS trạng thái thật |
| POST | `/api/payments/{id}/cancel` | Member, Receptionist, Manager | Hủy link payOS đang chờ |
| POST | `/api/payments/payos/webhook` | Public (chữ ký payOS) | payOS báo kết quả giao dịch |
| GET | `/api/payments/{id}/export-pdf` | Member, Receptionist, Manager | Tải biên lai PDF |

## Sports và Coaches — `/api`

`ClassCatalogController`. Mặc định `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/sports` | Public | Danh sách bộ môn |
| POST | `/api/sports` | Manager | Thêm bộ môn |
| PUT | `/api/sports/{id}` | Manager | Cập nhật bộ môn |
| POST | `/api/sports/{id}/image` | Manager | Upload ảnh bộ môn (jpg/png/webp, tối đa 2MB) |
| GET | `/api/coaches` | Public | Danh sách huấn luyện viên |
| PUT | `/api/coaches/{id}/profile` | Manager | Cập nhật hồ sơ chuyên môn huấn luyện viên |

## Coach Reviews — `/api/coaches`

`CoachReviewsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/coaches/{coachId}/reviews` | Public | Đánh giá của một huấn luyện viên (`page`, `pageSize`) |
| GET | `/api/coaches/mine/reviews` | Coach | Huấn luyện viên xem đánh giá về mình |
| POST | `/api/coaches/{coachId}/reviews` | Member | Hội viên đánh giá huấn luyện viên |
| PUT | `/api/coaches/{coachId}/reviews` | Member | Hội viên sửa đánh giá của mình |

## Classes — `/api/classes`

`SportClassesController`. Mặc định `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/classes` | Public | Tìm lớp đang hoạt động |
| GET | `/api/classes/manage` | Manager | Danh sách lớp (quản lý, có bộ lọc) |
| GET | `/api/classes/{id}` | Public | Chi tiết lớp |
| POST | `/api/classes` | Manager | Tạo lớp và phân công huấn luyện viên |
| PUT | `/api/classes/{id}` | Manager | Cập nhật lớp |
| POST | `/api/classes/{id}/image` | Manager | Upload ảnh lớp (jpg/png/webp, tối đa 2MB) |
| DELETE | `/api/classes/{id}` | Manager | Ngừng hoạt động lớp |
| GET | `/api/classes/{id}/sessions` | Public | Các buổi học của lớp |
| POST | `/api/classes/{id}/sessions` | Manager | Tạo buổi học |
| PUT | `/api/classes/{id}/sessions/{sessionId}` | Manager | Cập nhật lịch, huấn luyện viên, phòng của buổi học |

## Class Sessions — `/api/class-sessions`

`ClassSessionsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/class-sessions` | Public | Tìm buổi học (bộ lọc, phân trang) |
| GET | `/api/class-sessions/mine` | Coach | Lịch dạy của huấn luyện viên đang đăng nhập |
| POST | `/api/class-sessions/{id}/registrations` | Member | Đặt chỗ buổi học cho tôi |
| POST | `/api/class-sessions/{id}/registrations/members/{memberId}` | Receptionist, Manager | Đặt chỗ thay hội viên |
| GET | `/api/class-sessions/{id}/roster` | Coach, Receptionist, Manager | Danh sách học viên của buổi |
| POST | `/api/class-sessions/{id}/cancel` | Manager | Hủy cả buổi học |
| POST | `/api/class-sessions/{id}/complete` | Coach, Receptionist, Manager | Đánh dấu buổi học đã hoàn thành |
| GET | `/api/class-sessions/{id}/reviews` | Public | Đánh giá của buổi học (`page`, `pageSize`) |

## Class Registrations — `/api/class-registrations`

`ClassRegistrationsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/class-registrations/mine` | Member | Lịch sử đặt chỗ của tôi |
| GET | `/api/class-registrations/members/{memberId}` | Receptionist, Manager | Lịch sử đặt chỗ của một hội viên |
| POST | `/api/class-registrations/{id}/cancel` | Member, Receptionist, Manager | Hủy đặt chỗ, giải phóng chỗ |
| POST | `/api/class-registrations/{id}/check-in` | Member, Coach, Receptionist, Manager | Điểm danh theo lượt đặt chỗ |
| POST | `/api/class-registrations/{id}/review` | Member | Đánh giá buổi học đã tham gia |

Điều kiện đánh giá buổi học: buổi đã hoàn thành, hội viên có điểm danh `Present` hoặc `Late`, mỗi lượt đặt chỗ chỉ đánh giá một lần.

## Cancellation Policies — `/api/cancellation-policies`

`CancellationPoliciesController`. Chỉ `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/cancellation-policies` | Manager | Danh sách chính sách hủy |
| POST | `/api/cancellation-policies` | Manager | Tạo chính sách hủy |
| PUT | `/api/cancellation-policies/{id}` | Manager | Cập nhật chính sách hủy |

## Attendances — `/api/Attendances`

`AttendancesController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/Attendances/mark` | Manager, Receptionist, Coach | Ghi điểm danh học viên trong buổi |
| GET | `/api/Attendances/session/{sessionId}` | Manager, Receptionist, Coach | Danh sách điểm danh của buổi |
| GET | `/api/Attendances/mine` | Member | Lịch sử điểm danh của tôi |

## Rooms — `/api/Rooms`

`RoomsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/Rooms` | Đăng nhập | Danh sách phòng (lọc `isActive`) |
| GET | `/api/Rooms/{id}` | Đăng nhập | Chi tiết phòng |
| GET | `/api/Rooms/{id}/check-availability` | Đăng nhập | Phòng còn trống trong khoảng `startsAt`–`endsAt` không |
| POST | `/api/Rooms` | Manager | Tạo phòng |
| PUT | `/api/Rooms/{id}` | Manager | Cập nhật phòng |
| DELETE | `/api/Rooms/{id}` | Manager | Ngừng sử dụng phòng |

## Gym Check-ins — `/api/GymCheckIns`

`GymCheckInsController` (file `GymCheckInController.cs`).

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/GymCheckIns/my-qr` | Member | Lấy mã QR hội viên để check-in |
| POST | `/api/GymCheckIns/scan-qr` | Receptionist, Manager | Quét mã QR, ghi nhận hội viên vào trung tâm |
| POST | `/api/GymCheckIns/check-in` | Receptionist, Manager | Check-in bằng mã hội viên hoặc số điện thoại |
| PUT | `/api/GymCheckIns/{id}/check-out` | Receptionist, Manager | Ghi nhận hội viên rời trung tâm |
| GET | `/api/GymCheckIns/today` | Receptionist, Manager | Danh sách check-in hôm nay |
| GET | `/api/GymCheckIns/history` | Receptionist, Manager | Lịch sử check-in theo ngày, hội viên, từ khóa, trạng thái |
| GET | `/api/GymCheckIns/summary-by-member` | Receptionist, Manager | Tổng hợp check-in theo từng hội viên |
| GET | `/api/GymCheckIns/my-history` | Member | Lịch sử ra vào của tôi (`fromDate`, `toDate`) |

## Exercises — `/api/Exercises`

`ExercisesController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/Exercises` | Đăng nhập | Kho bài tập (`muscleGroup`, `search`) |
| GET | `/api/Exercises/muscle-groups` | Đăng nhập | Danh sách nhóm cơ cho bộ lọc |
| GET | `/api/Exercises/{id}` | Đăng nhập | Chi tiết bài tập |
| POST | `/api/Exercises` | Coach, Manager | Thêm bài tập |
| PUT | `/api/Exercises/{id}` | Coach, Manager | Sửa bài tập |
| DELETE | `/api/Exercises/{id}` | Coach, Manager | Xóa bài tập (chỉ khi chưa nằm trong giáo án nào) |

## Training Plans — `/api/TrainingPlans`

`TrainingPlansController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/TrainingPlans/exercises` | Đăng nhập | Danh mục bài tập để lập giáo án |
| POST | `/api/TrainingPlans/check-duplicate` | Coach | Kiểm tra giáo án mới có trùng giáo án hội viên đã hoàn thành không |
| POST | `/api/TrainingPlans` | Coach | Tạo giáo án |
| GET | `/api/TrainingPlans/members/{memberId}` | Coach, Member, Manager, Receptionist | Giáo án của một hội viên |
| PUT | `/api/TrainingPlans/exercises/{planExerciseId}/result` | Coach | Cập nhật kết quả và nhận xét bài tập |
| PUT | `/api/TrainingPlans/{id}/status` | Coach | Cập nhật trạng thái giáo án (`status`, mặc định `Completed`) |
| PUT | `/api/TrainingPlans/{id}` | Coach | Sửa giáo án |
| DELETE | `/api/TrainingPlans/{id}` | Coach | Xóa giáo án |
| POST | `/api/TrainingPlans/{id}/request-change` | Member | Hội viên xin đổi giáo án |

## AI — `/api/ai`

`AIController`. Dùng GroqCloud.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/ai/workout-recommendations` | Member, Coach | Gợi ý kế hoạch tập theo mục tiêu và trình độ |
| GET | `/api/ai/workout-recommendations/history` | Member, Coach | Lịch sử gợi ý |
| GET | `/api/ai/workout-recommendations/history/{id}` | Member, Coach | Chi tiết một gợi ý |
| DELETE | `/api/ai/workout-recommendations/history/{id}` | Member, Coach | Xóa một gợi ý |
| POST | `/api/ai/chat` | Member | Hỏi trợ lý AI về tập luyện và dịch vụ trung tâm |
| GET | `/api/ai/chat/history` | Member | Lịch sử chat |
| DELETE | `/api/ai/chat/history` | Member | Xóa lịch sử chat |

## Notifications — `/api/notifications`

`NotificationsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/notifications` | Đăng nhập | Thông báo của tôi (`unreadOnly`, `page`, `pageSize`) |
| PATCH | `/api/notifications/{id}/read` | Đăng nhập | Đánh dấu một thông báo đã đọc |

## Support Tickets — `/api/SupportTickets`

`SupportTicketsController`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/SupportTickets` | Member | Gửi yêu cầu hỗ trợ |
| GET | `/api/SupportTickets/my-tickets` | Member | Yêu cầu hỗ trợ của tôi |
| GET | `/api/SupportTickets` | Receptionist, Manager | Danh sách yêu cầu (`status`) |
| PUT | `/api/SupportTickets/{id}/resolve` | Receptionist, Manager | Phản hồi và đóng yêu cầu |
| POST | `/api/SupportTickets/{id}/image` | Member | Upload ảnh mô tả sự cố (jpg/png/webp, tối đa 2MB) |

## Reports — `/api/Reports`

`ReportsController`. Chỉ `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/Reports/overview` | Manager | Tổng quan hoạt động trung tâm |
| GET | `/api/Reports/revenue` | Manager | Doanh thu từng tháng theo năm (`year`) |
| GET | `/api/Reports/class-status` | Manager | Thống kê tình trạng đăng ký lớp |

## Audit Logs — `/api/audit-logs`

`AuditLogsController`. Chỉ `Manager`.

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/audit-logs` | Manager | Lịch sử thao tác hệ thống (bộ lọc, phân trang) |

## Khác

| Method | Path | Mô tả |
|---|---|---|
| GET | `/` | Chuyển hướng sang `/swagger` |
| GET | `/swagger` | Swagger UI (bật ở Development hoặc khi `OpenApi__Enabled=true`) |
