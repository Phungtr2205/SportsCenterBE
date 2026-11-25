# Ma trận lỗi (Exception Matrix)

Phạm vi: backend ASP.NET Core .NET 10, EF Core 10 + Npgsql, PostgreSQL trên Supabase.

Tài liệu tổng hợp **mọi loại lỗi API có thể trả về**: lỗi xảy ra ở đâu, mã HTTP nào, dữ liệu có bị ghi dở không, và client nên làm gì. Message trong bảng lấy nguyên văn từ code.

## Hợp đồng lỗi

Mọi lỗi đi qua `Middleware/ExceptionMiddleware.cs` và trả về cùng một dạng, với HTTP status bằng `statusCode`:

```json
{ "statusCode": 409, "message": "Buổi học đã hết chỗ." }
```

Ngoại lệ:

| Trường hợp | Dạng phản hồi |
|---|---|
| Sai kiểu dữ liệu, thiếu trường bắt buộc (DataAnnotations trên DTO) | `400` dạng `ProblemDetails` của ASP.NET Core, có trường `errors` theo tên field |
| Thiếu hoặc sai JWT | `401`, body rỗng (do JwtBearer trả về trước khi vào controller) |
| Sai role (`[Authorize(Roles = ...)]`) | `403`, body rỗng |
| `GET /api/payments/{id}/export-pdf` | `{ "message": "..." }` với `400` hoặc `404` |
| `POST /api/payments/payos/webhook` sai chữ ký | `ApiResponse` với `success: false`, `400` |
| Client tự ngắt kết nối giữa chừng | Không ghi log lỗi, không trả phản hồi |

**Không lộ thông tin nội bộ:** lỗi `500` luôn trả message chung `"An unexpected internal server error occurred. Please try again later."`. Chi tiết (stack trace, SQL) chỉ ghi vào log server qua `ILogger`.

## Ánh xạ exception sang mã HTTP

Thứ tự kiểm tra trong `ExceptionMiddleware.HandleExceptionAsync`:

| Exception | HTTP | Message trả về |
|---|---|---|
| `BusinessException(code, message)` | `code` | `message` |
| `DbUpdateConcurrencyException` | 409 | `exception.Message` |
| `DbUpdateException` với PostgreSQL `23505` (vi phạm unique) | 409 | `"The record already exists or was changed by another request. Reload and try again."` |
| `KeyNotFoundException` | 404 | `exception.Message` |
| `UnauthorizedAccessException` | 401 | `exception.Message` |
| `ArgumentException`, `ArgumentNullException`, `InvalidOperationException` | 400 | `exception.Message` |
| Còn lại | 500 | Message chung |

`BusinessException` (`Helpers/BusinessException.cs`) là cách chính để service báo lỗi nghiệp vụ. Toàn bộ code hiện có **172** message lỗi nghiệp vụ khác nhau.

## Tình huống và xử lý

| Nhóm / nơi xảy ra | Tình huống | HTTP | Dữ liệu và cách phục hồi |
|---|---|---|---|
| Mọi endpoint | DTO sai kiểu, thiếu trường, vượt độ dài | 400 | Từ chối trước khi vào service, không ghi gì |
| Mọi endpoint | Chưa đăng nhập, token hết hạn, sai chữ ký | 401 | Client gọi `/api/Auth/refresh-token`; refresh cũng lỗi thì đăng nhập lại |
| Mọi endpoint | Tài khoản bị khóa hoặc đổi role sau khi đã đăng nhập | 401 | Token cũ bị từ chối ngay ở `OnTokenValidated`; phải đăng nhập lại |
| Mọi endpoint | Sai role | 403 | Không gọi service |
| Dữ liệu của người khác | Member xem thanh toán, đăng ký gói, gợi ý AI của người khác | 403 hoặc 404 | Không tiết lộ dữ liệu; một số API trả 404 để không lộ bản ghi có tồn tại |
| Tìm theo id | Bản ghi không tồn tại hoặc đã ngừng hoạt động | 404 | Không ghi gì |
| Ràng buộc nghiệp vụ | Trạng thái không cho phép (hết chỗ, quá hạn hủy, gói không còn Pending...) | 409 | Transaction rollback; client tải lại dữ liệu |
| Database | Trùng khóa unique (email, số điện thoại, đặt chỗ trùng, 2 thanh toán Completed) | 409 | Transaction rollback |
| Database | Hai request ghi cùng lúc, dữ liệu đã đổi | 409 | Service bắt lỗi xung đột, trả `"Dữ liệu vừa thay đổi..."`; client tải lại và thử lại |
| Database | Transaction `Serializable` xung đột (`40001`) chưa được service bắt | 500 | Rollback, không ghi dở. **Chưa ánh xạ sang 409** trong middleware |
| Database | Mất kết nối, Supabase không phản hồi | 500 | Không ghi gì; kiểm tra chuỗi kết nối và trạng thái Supabase |
| payOS | Không gọi được payOS khi tạo, đồng bộ, hủy link | 503 | Giao dịch vẫn được lưu `Pending`; gọi `POST /api/payments/{id}/sync` |
| payOS | Kết quả payOS không khớp mã đơn / mã link | 502 hoặc 400 | Không cập nhật giao dịch |
| payOS | Có tiền về nhưng không kích hoạt được gói | 200 | Giao dịch chuyển `NeedsReview`; nhân viên đối soát thủ công |
| SendGrid | Thiếu cấu hình, sai API key, bị giới hạn gửi | 503 | OTP đã tạo nhưng email chưa gửi; gửi lại OTP |
| SendGrid | Lỗi HTTP khác, mất kết nối | 502 | Như trên |
| SendGrid | Quá 30 giây | 504 | Như trên. Không tự retry để tránh gửi email lặp |
| GroqCloud (AI) | Thiếu API key hoặc key sai định dạng | 503 | Không gọi AI |
| GroqCloud (AI) | Bị từ chối key, sai model, vượt hạn mức, AI lỗi 5xx | 503 | Thử lại sau |
| GroqCloud (AI) | Quá thời gian chờ (60 giây) | 504 | Thử lại |
| GroqCloud (AI) | AI trả kết quả sai định dạng | 502 | Không lưu lịch sử |
| Supabase Storage | Chưa cấu hình `SupabaseStorage` | 500 | Không upload |
| Supabase Storage | Upload thất bại | 502 | Ảnh cũ giữ nguyên |
| Google Login | Chưa cấu hình `GoogleAuth` | 503 | Dùng đăng nhập email/mật khẩu |
| Lỗi chưa biết | Exception ngoài các loại trên | 500 | Xem log server theo thời gian và đường dẫn request |

## Lỗi theo chức năng

### Xác thực và tài khoản

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Provide either idToken or code.` | Google login gửi cả hai hoặc không gửi gì |
| 400 | `redirectUri is required with code.` | Google login dùng `code` mà thiếu `redirectUri` |
| 400 | `Passwords do not match.` | Đặt lại mật khẩu, 2 ô mật khẩu khác nhau |
| 400 | `Date of birth cannot be in the future.` | Đăng ký với ngày sinh ở tương lai |
| 400 | `OTP is invalid or expired.` | OTP sai, hết 5 phút hoặc đã dùng |
| 400 | `Mật khẩu phải có ít nhất 8 ký tự và không được toàn khoảng trắng.` | |
| 400 | `Mật khẩu không được vượt quá 72 byte UTF-8.` | Giới hạn của BCrypt |
| 400 | `Mật khẩu hiện tại không chính xác.` | Đổi mật khẩu trong hồ sơ |
| 400 | `Mã xác nhận OTP không chính xác.` | Đổi email |
| 400 | `Bạn đã nhập sai mã xác nhận quá 5 lần. Vui lòng yêu cầu mã mới.` | Đổi email |
| 400 | `Email mới phải khác với email hiện tại.` | |
| 401 | `Invalid email or password.` | Đăng nhập sai |
| 401 | `Refresh token is invalid or expired.` | Refresh token sai, hết hạn, bị thu hồi |
| 401 | `Refresh token has already been used or revoked.` | Hai request refresh cùng lúc, request thua bị từ chối |
| 409 | `Email is already registered.` / `Email đã được đăng ký.` | |
| 409 | `Email này đã được sử dụng bởi một tài khoản khác.` | Đổi email trùng |
| 409 | `Số điện thoại đã được sử dụng.` | |
| 409 | `Bạn không thể tự đổi quyền của mình.` | Manager đổi role chính mình |
| 409 | `Bạn không thể vô hiệu hóa tài khoản của mình.` | |
| 503 | `Google login is not configured.` | |

### Gói tập và đăng ký gói

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Package name is required and must not exceed 200 characters.` | |
| 400 | `Price must be positive, fit decimal(18,2), and have at most two decimal places.` | |
| 400 | `Duration must be between 1 and 36500 days (100 years).` | |
| 400 | `PackageType must be 'Standard' or 'PT'.` | |
| 404 | `Không tìm thấy gói tập.` / `Membership package not found.` | |
| 404 | `Không tìm thấy gói tập muốn nâng cấp.` | |
| 409 | `Gói tập đã ngừng bán.` | Đăng ký gói đã bị ngừng |
| 409 | `Hội viên đã có yêu cầu đăng ký chờ thanh toán cho gói này.` | Bấm đăng ký 2 lần |
| 409 | `Hội viên đã có gói PT này đang hoạt động.` | |
| 409 | `Chỉ hủy đăng ký Pending chưa thanh toán.` | |
| 403 | `Bạn không có quyền hủy đăng ký gói.` | |

### Thanh toán

Chi tiết từng API: [api/payments.md](api/payments.md).

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Cần xác nhận đã nhận tiền trước khi lập biên lai.` | Lễ tân chưa tick "đã nhận tiền" |
| 400 | `Phương thức thanh toán phải là Cash, BankTransfer hoặc CreditCard.` | |
| 400 | `Số tiền phải bằng giá đã chốt của đăng ký gói.` | Lễ tân nhập sai số tiền |
| 400 | `Số tiền phải dương và có tối đa hai chữ số thập phân.` | |
| 403 | `Chỉ lễ tân hoặc quản lý được xác nhận thu tiền.` | |
| 409 | `Đăng ký gói đã được thanh toán.` | |
| 409 | `Giao dịch #... cần đối soát. Chưa thể thu thêm tiền hoặc hủy gói.` | Có giao dịch `NeedsReview` |
| 409 | `Giao dịch #... đang chờ thanh toán. Hãy đồng bộ hoặc hủy link payOS thành công trước.` | Còn link payOS `Pending` |
| 409 | `Đăng ký đã thanh toán hoặc có giao dịch cần đối soát.` | Tạo link payOS cho gói đã trả |
| 409 | `Chỉ hủy link payOS đang Pending.` | |
| 502 | `Kết quả payOS không khớp thanh toán.` | |
| 503 | `Chưa lấy được trạng thái payOS. Thử đồng bộ lại giao dịch này.` | |

### Lớp học, buổi học, đặt chỗ

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Buổi học phải bắt đầu trong tương lai và kết thúc sau giờ bắt đầu.` | Tạo hoặc sửa buổi |
| 400 | `Buổi học phải nằm trong khoảng thời gian của lớp.` | |
| 409 | `HLV đã có buổi học khác trùng giờ.` | Xếp lịch trùng cho huấn luyện viên |
| 409 | `Phòng tập đã có buổi học khác trong khung giờ này.` | Xếp lịch trùng phòng |
| 409 | `Số chỗ không thể thấp hơn số học viên đã đăng ký.` | Giảm sức chứa |
| 409 | `Không sửa buổi học đã bắt đầu, hủy hoặc hoàn thành.` | |
| 409 | `Hãy hủy hoặc hoàn thành các buổi học trước khi đóng lớp.` | |
| 409 | `Không đổi bộ môn khi đã có học viên đăng ký.` | |
| 409 | `Hội viên cần có gói tập còn hạn để đặt lớp.` | |
| 409 | `Buổi học đã hết chỗ.` | |
| 409 | `Hội viên đã đăng ký buổi học này.` | |
| 409 | `Buổi học bị trùng giờ với lịch đã đăng ký.` | Hội viên đặt 2 buổi trùng giờ |
| 409 | `Buổi học không còn nhận đăng ký.` | Buổi đã bắt đầu, hủy hoặc hoàn thành |
| 409 | `Đã quá hạn hủy hoặc đăng ký không còn có thể hủy.` | Hủy sau hạn của chính sách hủy |
| 409 | `Không hủy đăng ký đã điểm danh.` | |
| 409 | `Chỉ hủy buổi học chưa bắt đầu.` | Manager hủy cả buổi |
| 409 | `Chỉ hoàn thành buổi học đã kết thúc và chưa bị hủy.` | |

### Điểm danh và check-in

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Status phải là Present, Absent, Late hoặc Excused.` | |
| 403 | `Bạn không có quyền thao tác điểm danh buổi học này.` | Huấn luyện viên điểm danh buổi không phải của mình |
| 409 | `Chưa đến thời gian điểm danh buổi học này (chỉ mở trước giờ bắt đầu 30 phút).` | |
| 409 | `Đã quá thời hạn điểm danh cho buổi học này.` | |
| 409 | `Không được điểm danh buổi học đã hủy.` | |
| 409 | `Hội viên chưa đăng ký buổi học này hoặc đã hủy đăng ký.` | |

### Đánh giá

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Số sao đánh giá phải từ 1 đến 5.` | |
| 400 | `Nhận xét tối đa 2000 ký tự.` | |
| 403 | `Chỉ hội viên được đánh giá.` / `Chỉ hội viên được đánh giá huấn luyện viên.` | |
| 409 | `Chỉ đánh giá buổi đã hoàn thành và có điểm danh tham gia.` | |
| 409 | `Bạn đã đánh giá buổi học này.` | |
| 409 | `Bạn đã đánh giá huấn luyện viên này.` | Dùng `PUT` để sửa |
| 404 | `Bạn chưa đánh giá huấn luyện viên này.` | Sửa đánh giá chưa có |

### Giáo án và bài tập

| HTTP | Message | Khi nào |
|---|---|---|
| 403 | `Chỉ dành cho HLV.` | |
| 403 | `Bạn chỉ được xem giáo án của chính mình.` | Member xem giáo án của hội viên khác |
| 409 | `Chỉ có thể chỉnh sửa giáo án đang áp dụng (Active).` | |
| 409 | `Chỉ có thể yêu cầu điều chỉnh giáo án đang áp dụng.` | |
| 409 | `Giáo án này chưa có huấn luyện viên phụ trách.` | Hội viên xin đổi giáo án |
| 409 | `Bài tập này đang được sử dụng trong các giáo án học viên, không thể xóa.` | |

### AI

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Vui lòng nhập mục tiêu tập luyện.` | |
| 400 | `Coach cần chọn học viên mình phụ trách.` | Huấn luyện viên gọi gợi ý mà không chọn hội viên |
| 503 | `AI đang vượt giới hạn yêu cầu hoặc hạn mức sử dụng. Vui lòng thử lại sau.` | GroqCloud trả 429 |
| 503 | `Dịch vụ AI đang gián đoạn. Vui lòng thử lại sau.` | GroqCloud lỗi 5xx |
| 504 | `AI phản hồi quá lâu. Vui lòng thử lại.` | |
| 502 | `Bản gợi ý đã lưu không đúng định dạng.` | |

### Upload ảnh

| HTTP | Message | Khi nào |
|---|---|---|
| 400 | `Chỉ chấp nhận ảnh .jpg, .png, .webp.` | |
| 400 | `Ảnh phải có dung lượng từ 1 byte đến 2MB.` | |
| 500 | `Supabase Storage chưa được cấu hình.` | |
| 502 | `Upload ảnh lên Supabase thất bại.` | |

### Phân trang và báo cáo

| HTTP | Message |
|---|---|
| 400 | `Page từ 1 đến 100000; PageSize từ 1 đến 100.` |
| 400 | `Khoảng ngày không hợp lệ. Dùng năm từ 2000 đến 2100 và From <= To.` |
| 400 | `Mỗi lần xem tối đa 366 ngày.` |
| 400 | `Cần truyền cả from và to.` |

## Hạn chế hiện tại

- Lỗi `40001` (transaction `Serializable` xung đột) chưa được `ExceptionMiddleware` ánh xạ sang `409`, nên trả `500` dù dữ liệu không bị ghi dở.
- Chưa có mã lỗi dạng chuỗi (ví dụ `PAYMENT_NEEDS_REVIEW`); frontend phải dựa vào HTTP status và message.
- Message lẫn tiếng Anh và tiếng Việt giữa các service.
- Chưa giới hạn số lần gọi (rate limit) cho các API gửi OTP.
- Chưa có API xử lý giao dịch `NeedsReview`.
