# Kế hoạch kiểm thử theo hành trình người dùng

Tài liệu liệt kê kịch bản kiểm thử end-to-end cho từng vai trò, kèm API cần gọi, kết quả mong đợi và các nhánh lỗi phải thử. Dùng cho kiểm thử thủ công trên Swagger hoặc frontend, và làm kịch bản demo.

## Nguyên tắc

1. **Chạy theo chuỗi:** id, token, payment của bước trước dùng cho bước sau. Không chỉ kiểm từng API riêng lẻ trả `200`.
2. **Kiểm cả dữ liệu sau mỗi bước**, không chỉ HTTP status. Ví dụ đặt chỗ xong thì số chỗ còn lại phải giảm, hủy xong phải tăng lại.
3. **Mỗi bước thử ít nhất một nhánh lỗi:** sai role, dữ liệu của người khác, trạng thái không hợp lệ, gửi lặp.
4. **Dùng database riêng để test.** Không thử trên database đang demo cho giảng viên. Chỉ xóa dữ liệu do lượt test tạo ra.
5. **payOS dùng môi trường test**, số tiền nhỏ. Không thử giao dịch thật.
6. Ghi kết quả vào cột **Kết quả** (Đạt / Không đạt / Chưa chạy) và mô tả lỗi nếu có.

## Môi trường và dữ liệu

| Hạng mục | Giá trị |
|---|---|
| API | `https://localhost:7182/swagger` (local) hoặc URL Render |
| Database | PostgreSQL riêng cho test, `Database:InitializeOnStartup = true` lần đầu để chạy migration và seed |
| Môi trường | `Development` (seed chỉ chạy ở Development) |

**Tài khoản mẫu** (`Data/DbSeeder.cs`, mật khẩu chung `Password@123`):

| Vai trò | Email | Ghi chú |
|---|---|---|
| Receptionist | `receptionist@sportscenter.com` | |
| Coach | `coach1@sportscenter.com` | |
| Member | `member1@gmail.com` | Có sẵn gói `Gold All-Access` đang Active |
| Manager | — | **Seed chưa tạo.** Tạo thủ công trong database (đổi `Role` một tài khoản thành `Manager`) trước khi test các hành trình của Manager |

**Dữ liệu seed:** 8 bài tập, 4 gói (`Flex Pass - Tự Do` 800.000đ/30 ngày, `Gold All-Access` 4.500.000đ/180 ngày, `Olympus VIP Club` 12.000.000đ/365 ngày, `PT Pro Training 1-1` 6.000.000đ/90 ngày), bộ môn `Yoga`. Seed **không** tạo phòng, lớp, buổi học: Manager phải tạo ở hành trình J04.

**Thứ tự chạy đề xuất:** J01 → J04 → J02 → J03 → J05 → J06 → J07 → J08 → J09 → J10.

---

## J01 — Tài khoản và phiên đăng nhập

Mở app → đăng ký → đăng nhập → xem hồ sơ → làm mới token → đăng xuất.

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 1.1 | Gửi OTP đăng ký với email mới | `POST /api/Auth/send-register-otp` | 200, email nhận mã 6 số | |
| 1.2 | Nhập OTP đúng | `POST /api/Auth/verify-register-otp` | 200, trả access token + refresh token, tạo User role `Member` và hồ sơ Member | |
| 1.3 | Đăng nhập | `POST /api/Auth/login` | 200, có `accessToken`, `refreshTokenKey` | |
| 1.4 | Xem hồ sơ | `GET /api/profile`, `GET /api/members/me` | 200, đúng tên, email | |
| 1.5 | Làm mới token | `POST /api/Auth/refresh-token` | 200, refresh token mới khác token cũ | |
| 1.6 | Đăng xuất | `POST /api/Auth/logout` | 200; dùng lại refresh token cũ bị 401 | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 1.E1 | Đăng ký email đã tồn tại | 409 `Email is already registered.` | |
| 1.E2 | Nhập OTP sai 5 lần, lần 6 nhập đúng | Vẫn 400, phải xin mã mới | |
| 1.E3 | Nhập OTP sau 5 phút | 400 `OTP is invalid or expired.` | |
| 1.E4 | Ngày sinh ở tương lai | 400 | |
| 1.E5 | Mật khẩu dưới 8 ký tự | 400 | |
| 1.E6 | Đăng nhập sai mật khẩu | 401 `Invalid email or password.` | |
| 1.E7 | Gọi API không có token | 401 | |
| 1.E8 | Dùng lại refresh token cũ trong 30 giây sau khi đã xoay | 200 (cho phép, phòng mất phản hồi) | |
| 1.E9 | Dùng lại refresh token cũ sau 30 giây | 401 | |
| 1.E10 | Manager khóa tài khoản khi người dùng đang đăng nhập, người dùng gọi API tiếp | 401 ngay, không đợi token hết hạn | |
| 1.E11 | Quên mật khẩu: OTP → đặt mật khẩu mới, 2 ô mật khẩu khác nhau | 400 `Passwords do not match.` | |

Tự động hóa: `AuthRefreshTokenTests`, `MemoryOtpStoreTests`, `ProfileServiceTests`.

## J02 — Hội viên mua gói và thanh toán payOS

Xem gói → đăng ký gói → tạo link payOS → chuyển khoản → gói Active → tải biên lai.

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 2.1 | Xem danh sách gói (không đăng nhập) | `GET /api/membership-packages` | 200, chỉ gói đang bán | |
| 2.2 | Đăng ký gói `Flex Pass` | `POST /api/member-subscriptions` | 200/201, Subscription `Pending`, `AgreedPrice = 800000` | |
| 2.3 | Tạo link thanh toán | `POST /api/payments` | Payment `Pending`, có `checkoutUrl`, `qrCode`, `payOSOrderCode` | |
| 2.4 | Bấm tạo link lần nữa (giả lập mở 2 tab) | `POST /api/payments` | Trả **cùng** Payment, cùng `payOSOrderCode` | |
| 2.5 | Chuyển khoản trên trang payOS test | — | payOS gọi webhook | |
| 2.6 | Đồng bộ trạng thái | `POST /api/payments/{id}/sync` | Payment `Completed`, có `transactionReference` `INV-...`, `paidAt` | |
| 2.7 | Xem gói của tôi | `GET /api/member-subscriptions/mine` | Gói `Active`, `EndDate = StartDate + 30 ngày` | |
| 2.8 | Xem thông báo | `GET /api/notifications` | Có thông báo "Thanh toán thành công" | |
| 2.9 | Tải biên lai | `GET /api/payments/{id}/export-pdf` | File PDF, đúng tên gói, số tiền, giờ UTC+7 | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 2.E1 | Đăng ký cùng gói 2 lần khi lần đầu còn Pending | 409 `Hội viên đã có yêu cầu đăng ký chờ thanh toán cho gói này.` | |
| 2.E2 | Tạo link cho gói đã thanh toán | 409 | |
| 2.E3 | Member A gọi `GET /api/payments/{id}` của Member B | 403 | |
| 2.E4 | Member A tải biên lai của Member B | 404 | |
| 2.E5 | Tải biên lai khi Payment chưa Completed | 400 | |
| 2.E6 | payOS gửi webhook 2 lần cho cùng giao dịch | Gói chỉ kích hoạt 1 lần, chỉ 1 thông báo | |
| 2.E7 | Hủy link đang Pending với lý do rỗng | 400 | |
| 2.E8 | Hủy link đang Pending | Payment `Cancelled`; tạo link mới được, `payOSOrderCode` mới | |
| 2.E9 | Manager đổi giá gói sau khi khách đã đăng ký Pending | Thanh toán vẫn theo `AgreedPrice` cũ | |
| 2.E10 | Webhook có chữ ký sai | 400, không đổi dữ liệu | |

Tự động hóa: `PaymentWebhookTests` (đủ tiền → Completed; sai tiền → NeedsReview; gửi lặp không kích hoạt 2 lần), `SubscriptionServiceTests`.

## J03 — Lễ tân đăng ký và thu tiền tại quầy

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 3.1 | Tạo tài khoản hội viên tại quầy | `POST /api/members` | Hội viên mới | |
| 3.2 | Đăng ký gói thay hội viên | `POST /api/member-subscriptions/members/{memberId}` | Subscription `Pending` | |
| 3.3 | Xác nhận thu tiền mặt đúng giá | `POST /api/member-subscriptions/{id}/payments` (`Cash`, `paymentReceived: true`) | 201, gói `Active`, có biên lai | |
| 3.4 | Xem lịch sử thanh toán | `GET /api/payments` | Thấy giao dịch vừa thu | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 3.E1 | Nhập số tiền khác giá đã chốt | 400 `Số tiền phải bằng giá đã chốt của đăng ký gói.` | |
| 3.E2 | `paymentReceived: false` | 400 | |
| 3.E3 | Hội viên đang có link payOS Pending cho gói đó, lễ tân thu tiền mặt | 409 `Giao dịch #... đang chờ thanh toán...` | |
| 3.E4 | Thu tiền lần 2 cho gói đã Active | 409 | |
| 3.E5 | Member gọi API xác nhận thu tiền | 403 | |

**Nâng cấp gói**

| # | Bước | Mong đợi | Kết quả |
|---|---|---|---|
| 3.U1 | `GET /api/member-subscriptions/upgrade-preview` từ `Flex Pass` lên `Gold All-Access` | Số tiền chênh lệch hợp lý | |
| 3.U2 | Đăng ký và thanh toán gói cao hơn | Gói mới Active, `StartDate` = `StartDate` gói cũ; gói cũ `Upgraded` | |
| 3.U3 | Đăng ký gói thấp hơn khi đang có gói cao hơn | 409 | |
| 3.U4 | Đăng ký gói PT song song với gói Standard | Thành công, giá đầy đủ | |

Tự động hóa: `SubscriptionServiceTests`.

## J04 — Manager thiết lập danh mục và lịch học

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 4.1 | Tạo phòng | `POST /api/Rooms` | Phòng Active | |
| 4.2 | Tạo bộ môn, upload ảnh | `POST /api/sports`, `POST /api/sports/{id}/image` | Có `imageUrl` trên Supabase | |
| 4.3 | Tạo chính sách hủy (ví dụ trước 2 giờ) | `POST /api/cancellation-policies` | | |
| 4.4 | Tạo lớp, phân công `coach1` | `POST /api/classes` | | |
| 4.5 | Tạo 2 buổi học trong tương lai | `POST /api/classes/{id}/sessions` | | |
| 4.6 | Tạo một buổi trong quá khứ gần để test J06 | (tạo rồi chỉnh thời gian trong database test) | | |
| 4.7 | Kiểm tra phòng trống | `GET /api/Rooms/{id}/check-availability` | `false` trong giờ đã xếp | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 4.E1 | Xếp 2 buổi trùng giờ cùng phòng | 409 `Phòng tập đã có buổi học khác trong khung giờ này.` | |
| 4.E2 | Xếp 2 buổi trùng giờ cùng huấn luyện viên | 409 `HLV đã có buổi học khác trùng giờ.` | |
| 4.E3 | Buổi học bắt đầu ở quá khứ | 400 | |
| 4.E4 | Giảm sức chứa thấp hơn số người đã đặt | 409 | |
| 4.E5 | Upload file `.gif` hoặc ảnh > 2MB | 400 | |
| 4.E6 | Receptionist gọi API tạo lớp | 403 | |
| 4.E7 | Tạo phòng trùng tên (khác hoa thường) | 400 | |

Tự động hóa: `ClassServiceTests`, `RoomServiceTests`.

## J05 — Hội viên đặt và hủy buổi học

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 5.1 | Tìm buổi học | `GET /api/class-sessions` | Thấy buổi vừa tạo, số chỗ còn lại | |
| 5.2 | Đặt chỗ | `POST /api/class-sessions/{id}/registrations` | Registration `Registered`, có `cancellationDeadline` | |
| 5.3 | Xem lịch của tôi | `GET /api/class-registrations/mine` | Có buổi vừa đặt | |
| 5.4 | Hủy trước hạn | `POST /api/class-registrations/{id}/cancel` | `Cancelled`, số chỗ tăng lại | |
| 5.5 | Lễ tân đặt thay hội viên | `POST /api/class-sessions/{id}/registrations/members/{memberId}` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 5.E1 | Hội viên không có gói còn hạn đặt chỗ | 409 `Hội viên cần có gói tập còn hạn để đặt lớp.` | |
| 5.E2 | Đặt cùng buổi 2 lần | 409 `Hội viên đã đăng ký buổi học này.` | |
| 5.E3 | Đặt 2 buổi trùng giờ | 409 `Buổi học bị trùng giờ với lịch đã đăng ký.` | |
| 5.E4 | Buổi đã đủ chỗ | 409 `Buổi học đã hết chỗ.` | |
| 5.E5 | Hủy sau hạn hủy | 409 `Đã quá hạn hủy...` | |
| 5.E6 | Member A hủy chỗ của Member B | 403 hoặc 404 | |
| 5.E7 | Manager hủy cả buổi | Mọi người đã đặt nhận thông báo, đặt chỗ bị hủy | |

## J06 — Huấn luyện viên điểm danh, hội viên đánh giá

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 6.1 | Huấn luyện viên xem lịch dạy | `GET /api/class-sessions/mine` | | |
| 6.2 | Xem danh sách học viên | `GET /api/class-sessions/{id}/roster` | | |
| 6.3 | Điểm danh `Present` | `POST /api/Attendances/mark` | Attendance được tạo, hội viên nhận thông báo | |
| 6.4 | Hoàn thành buổi (sau giờ kết thúc) | `POST /api/class-sessions/{id}/complete` | Session và các Registration `Completed` | |
| 6.5 | Hội viên đánh giá buổi | `POST /api/class-registrations/{id}/review` | Review được tạo | |
| 6.6 | Hội viên đánh giá huấn luyện viên | `POST /api/coaches/{coachId}/reviews` | | |
| 6.7 | Xem điểm huấn luyện viên | `GET /api/coaches`, `GET /api/coaches/{coachId}/reviews` | Điểm trung bình và số đánh giá đúng | |
| 6.8 | Hội viên xem lịch sử điểm danh | `GET /api/Attendances/mine` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 6.E1 | Điểm danh sớm hơn 30 phút trước giờ học | 409 | |
| 6.E2 | Điểm danh buổi đã hủy | 409 | |
| 6.E3 | Điểm danh hội viên không đặt buổi đó | 409 | |
| 6.E4 | Huấn luyện viên khác điểm danh buổi không phải của mình | 403 | |
| 6.E5 | Status không hợp lệ (ví dụ `Here`) | 400 | |
| 6.E6 | Đánh giá buổi khi bị điểm danh `Absent` | 409 | |
| 6.E7 | Đánh giá buổi lần 2 | 409 `Bạn đã đánh giá buổi học này.` | |
| 6.E8 | Đánh giá huấn luyện viên chưa từng dạy mình | 409 | |
| 6.E9 | Đánh giá 6 sao | 400 | |

Tự động hóa: `AttendanceServiceTests`, `CoachReviewServiceTests`.

## J07 — Check-in vào trung tâm

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 7.1 | Hội viên lấy mã QR | `GET /api/GymCheckIns/my-qr` | `qrData = SC-MEM-000xxx`, `packageStatus = Active` | |
| 7.2 | Lễ tân quét mã | `POST /api/GymCheckIns/scan-qr` | Tạo lượt check-in | |
| 7.3 | Xem danh sách hôm nay | `GET /api/GymCheckIns/today` | Có hội viên vừa vào | |
| 7.4 | Check-out | `PUT /api/GymCheckIns/{id}/check-out` | Có `checkOutTime` và thời gian ở lại | |
| 7.5 | Check-in bằng số điện thoại | `POST /api/GymCheckIns/check-in` | | |
| 7.6 | Hội viên xem lịch sử | `GET /api/GymCheckIns/my-history` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 7.E1 | Hội viên không có gói còn hạn | 400 `Hội viên không có gói tập nào đang hoạt động...` | |
| 7.E2 | Check-in 2 lần chưa check-out | 400 `Hội viên này đã check-in trước đó và chưa check-out!` | |
| 7.E3 | Check-out 2 lần | 400 | |
| 7.E4 | Mã QR không tồn tại | 404 | |

Tự động hóa: `GymCheckInServiceTests`.

## J08 — Giáo án tập luyện (PT)

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 8.1 | Huấn luyện viên xem kho bài tập | `GET /api/Exercises` | 8 bài tập seed | |
| 8.2 | Kiểm tra trùng giáo án cũ | `POST /api/TrainingPlans/check-duplicate` | | |
| 8.3 | Tạo giáo án cho hội viên | `POST /api/TrainingPlans` | Giáo án `Active` | |
| 8.4 | Hội viên xem giáo án | `GET /api/TrainingPlans/members/{memberId}` | | |
| 8.5 | Cập nhật kết quả bài tập | `PUT /api/TrainingPlans/exercises/{planExerciseId}/result` | | |
| 8.6 | Hội viên xin đổi giáo án | `POST /api/TrainingPlans/{id}/request-change` | Huấn luyện viên nhận thông báo | |
| 8.7 | Sửa giáo án | `PUT /api/TrainingPlans/{id}` | | |
| 8.8 | Hoàn thành giáo án | `PUT /api/TrainingPlans/{id}/status?status=Completed` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 8.E1 | Huấn luyện viên khác sửa hoặc xóa giáo án | Bị chặn | |
| 8.E2 | Tạo giáo án trùng giáo án đã hoàn thành mà không xác nhận | 409 cảnh báo | |
| 8.E3 | Sửa giáo án đã `Completed` | 409 | |
| 8.E4 | Xóa bài tập đang có trong giáo án | 409 | |
| 8.E5 | Member A xem giáo án của Member B | 403 `Bạn chỉ được xem giáo án của chính mình.` | |

Tự động hóa: `TrainingPlanServiceTests`.

## J09 — AI tư vấn

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 9.1 | Xin gợi ý bài tập theo mục tiêu | `POST /api/ai/workout-recommendations` | Kế hoạch bằng tiếng Việt, phù hợp tuổi, giới tính, mục tiêu trong hồ sơ | |
| 9.2 | Xem lịch sử gợi ý | `GET /api/ai/workout-recommendations/history` | | |
| 9.3 | Chat hỏi về gói tập | `POST /api/ai/chat` | Trả lời đúng gói đang bán | |
| 9.4 | Chat hỏi lịch học của tôi | `POST /api/ai/chat` | Trả lời đúng lịch đã đặt | |
| 9.5 | Xóa lịch sử chat | `DELETE /api/ai/chat/history` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 9.E1 | Mục tiêu để trống | 400 | |
| 9.E2 | Mục tiêu vô nghĩa hoặc không liên quan tập luyện | Bị từ chối | |
| 9.E3 | Huấn luyện viên xin gợi ý mà không chọn hội viên | 400 | |
| 9.E4 | Sai hoặc thiếu `AI:ApiKey` | 503 | |
| 9.E5 | Receptionist gọi chat | 403 | |

## J10 — Manager vận hành

| # | Bước | API | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| 10.1 | Tạo tài khoản lễ tân, huấn luyện viên | `POST /api/users/staff` | Có AuditLog | |
| 10.2 | Đổi role nhân viên | `PUT /api/users/staff/{id}/role` | Nhân viên đang đăng nhập bị 401 ngay ở request tiếp theo | |
| 10.3 | Vô hiệu hóa hội viên | `DELETE /api/members/{id}` | Hội viên không đăng nhập được | |
| 10.4 | Xem báo cáo | `GET /api/Reports/overview`, `/revenue?year=2026`, `/class-status` | Doanh thu khớp các giao dịch `Completed` đã tạo ở J02, J03 | |
| 10.5 | Xem audit log | `GET /api/audit-logs` | Thấy các thao tác trên | |
| 10.6 | Xử lý phiếu hỗ trợ | `GET /api/SupportTickets`, `PUT /api/SupportTickets/{id}/resolve` | | |

**Nhánh lỗi**

| # | Tình huống | Mong đợi | Kết quả |
|---|---|---|---|
| 10.E1 | Manager tự đổi role của mình | 409 | |
| 10.E2 | Manager tự vô hiệu hóa mình | 409 | |
| 10.E3 | Tạo nhân viên trùng email | 409 | |
| 10.E4 | Receptionist xem báo cáo | 403 | |

Tự động hóa: `AccountServiceTests`.

---

## Kiểm thử tự động hiện có

Chạy: `dotnet test SportsCenterAPI.slnx`. CI GitHub Actions (`ci.yml`) chạy khi push vào `master`, `unittest*`, `nguyen*` và khi mở PR vào `master`.

| File test | Số test | Phạm vi |
|---|---|---|
| `AccountServiceTests` | 6 | Tạo, đổi role, vô hiệu hóa nhân viên và hội viên |
| `AttendanceServiceTests` | 6 | Điểm danh và các nhánh lỗi |
| `AuthRefreshTokenTests` | 4 | Xoay refresh token, 30 giây cho phép, token bị thu hồi |
| `ClassServiceTests` | 2 | Tạo lớp |
| `CoachReviewServiceTests` | 6 | Đánh giá huấn luyện viên, điểm trung bình |
| `GymCheckInServiceTests` | 6 | QR, check-in, check-out |
| `MemoryOtpStoreTests` | 7 | OTP: hết hạn, sai 5 lần, dùng 1 lần, gửi lại, đồng thời |
| `PaymentWebhookTests` | 3 | Webhook payOS: đủ tiền, sai tiền, gửi lặp |
| `ProfileServiceTests` | 4 | Hồ sơ, đổi email |
| `RoomServiceTests` | 6 | Phòng, trùng tên, kiểm tra trống |
| `SubscriptionServiceTests` | 8 | Đăng ký, nâng cấp gói, thu tiền tại quầy |
| `TrainingPlanServiceTests` | 9 | Giáo án: sửa, xóa, trùng, xin đổi, quyền xem, hội viên tự hoàn thành |
| **Tổng** | **67** | |

## Chưa có test tự động

| Phạm vi | Lý do / rủi ro |
|---|---|
| Đặt chỗ, hủy chỗ, hết chỗ, trùng giờ (`ClassService.Bookings`) | Logic nhiều nhánh, chỉ kiểm thủ công |
| Race condition thật (2 request đặt chỗ cuối cùng cùng lúc) | Test dùng EF Core InMemory, không có transaction `Serializable` và index có điều kiện của PostgreSQL |
| `POST /api/payments`, `sync`, `cancel` | Cần giả lập payOS |
| Hai giao dịch cùng báo thành công cho một gói | Chưa có test |
| AI (`AIService`) | Phụ thuộc GroqCloud |
| Báo cáo doanh thu | Chưa có test |
