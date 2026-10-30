using System.Data;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Subscriptions;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _context;

    public SubscriptionService(AppDbContext context)
    {
        _context = context;
    }

    // Đăng ký gói tập (hỗ trợ mua mới, mua song song gói PT, hoặc nâng cấp bù trừ chênh lệch)
    public async Task<SubscriptionDTO> CreateAsync(
        int actorId,
        int? memberId,
        CreateSubscriptionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        return await InTransactionAsync(async () =>
        {
            var member = await ResolveMemberAsync(
                actorId,
                memberId,
                cancellationToken);

            var package = await _context.MembershipPackages
                .SingleOrDefaultAsync(
                    package => package.Id == request.PackageId,
                    cancellationToken);

            if (package == null)
            {
                throw new BusinessException(
                    404,
                    "Không tìm thấy gói tập.");
            }

            if (!package.IsActive)
            {
                throw new BusinessException(
                    409,
                    "Gói tập đã ngừng bán.");
            }

            if (package.Price <= 0 ||
                decimal.Round(package.Price, 2) != package.Price ||
                package.DurationInDays <= 0 ||
                package.DurationInDays > 36500)
            {
                throw new BusinessException(
                    409,
                    "Giá hoặc thời hạn gói tập không hợp lệ.");
            }

            var now = DateTime.UtcNow;
            var targetPackageType = PackageTypeHelper.GetPackageType(package.PackageName, package.Description);

            // 1. Chặn nếu đang có đơn PENDING cho chính gói này
            var hasPendingThisPackage = await _context.MemberSubscriptions
                .AnyAsync(s => s.MemberId == member.Id && s.PackageId == package.Id && s.Status == "Pending", cancellationToken);
            if (hasPendingThisPackage)
            {
                throw new BusinessException(409, "Hội viên đã có yêu cầu đăng ký chờ thanh toán cho gói này.");
            }

            // 2. Tìm các gói đang Active của hội viên
            var activeSubs = await _context.MemberSubscriptions
                .Include(s => s.Package)
                .Where(s => s.MemberId == member.Id && s.Status == "Active" && (s.EndDate == null || s.EndDate > now))
                .ToListAsync(cancellationToken);

            // Tìm gói cùng loại (Standard hoặc PT) đang còn hiệu lực
            var currentSameTypeSub = activeSubs.FirstOrDefault(s =>
                PackageTypeHelper.GetPackageType(s.Package.PackageName, s.Package.Description) == targetPackageType);

            decimal agreedPrice = package.Price;
            string auditDetails;

            if (currentSameTypeSub != null)
            {
                if (targetPackageType == PackageTypeHelper.Standard)
                {
                    // Đang có gói tập cơ bản (Standard) còn hạn -> NÂNG CẤP GÓI
                    if (package.Price <= currentSameTypeSub.AgreedPrice)
                    {
                        throw new BusinessException(409,
                            $"Hội viên đang sử dụng gói {currentSameTypeSub.Package.PackageName}. Chỉ có thể nâng cấp lên gói có giá trị cao hơn.");
                    }

                    // TÍNH TIỀN CHÊNH LỆCH BÙ THÊM
                    agreedPrice = package.Price - currentSameTypeSub.AgreedPrice;
                    auditDetails = $"Tạo yêu cầu NÂNG CẤP gói từ #{currentSameTypeSub.Id} ({currentSameTypeSub.Package.PackageName}) lên #{package.Id} ({package.PackageName}), số tiền chênh lệch: {agreedPrice:N0}đ.";
                }
                else
                {
                    // Gói PT: Chặn mua trùng chính gói PT đang hoạt động
                    if (currentSameTypeSub.PackageId == package.Id)
                    {
                        throw new BusinessException(409, "Hội viên đã có gói PT này đang hoạt động.");
                    }
                    auditDetails = $"Tạo đăng ký gói PT {package.Id} ({package.PackageName}) cho hội viên {member.Id}.";
                }
            }
            else
            {
                // Mua mới hoặc mua song song gói khác loại (ví dụ đang có Standard mua thêm gói PT)
                auditDetails = $"Tạo đăng ký Pending cho hội viên {member.Id}, gói {package.Id} ({targetPackageType}).";
            }

            var subscription = new MemberSubscription
            {
                MemberId = member.Id,
                Package = package,
                AgreedPrice = agreedPrice,
                DurationInDays = package.DurationInDays,
                CreatedAt = now,
                Status = "Pending"
            };

            _context.MemberSubscriptions.Add(subscription);
            await _context.SaveChangesAsync(cancellationToken);

            AddAudit(
                actorId,
                "Create",
                nameof(MemberSubscription),
                subscription.Id,
                auditDetails);

            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(subscription, now);
        }, cancellationToken);
    }

    // Xem trước thông tin nâng cấp gói và tính tiền chênh lệch
    public async Task<UpgradePreviewDTO> GetUpgradePreviewAsync(
        int actorId,
        int? memberId,
        int targetPackageId,
        CancellationToken cancellationToken = default)
    {
        var member = await ResolveMemberAsync(
            actorId,
            memberId,
            cancellationToken,
            requireActive: true);

        var targetPackage = await _context.MembershipPackages
            .SingleOrDefaultAsync(
                p => p.Id == targetPackageId && p.IsActive,
                cancellationToken)
            ?? throw new BusinessException(404, "Không tìm thấy gói tập muốn nâng cấp.");

        var now = DateTime.UtcNow;
        var targetType = PackageTypeHelper.GetPackageType(targetPackage.PackageName, targetPackage.Description);

        var activeSubs = await _context.MemberSubscriptions
            .Include(s => s.Package)
            .Where(s => s.MemberId == member.Id && s.Status == "Active" && (s.EndDate == null || s.EndDate > now))
            .ToListAsync(cancellationToken);

        var currentSameType = activeSubs.FirstOrDefault(s =>
            PackageTypeHelper.GetPackageType(s.Package.PackageName, s.Package.Description) == targetType);

        if (currentSameType == null)
        {
            return new UpgradePreviewDTO
            {
                CanUpgrade = false,
                Message = "Hội viên chưa có gói cùng loại đang hoạt động. Sẽ đăng ký mới với giá đầy đủ.",
                TargetPackageId = targetPackage.Id,
                TargetPackageName = targetPackage.PackageName,
                TargetPackagePrice = targetPackage.Price,
                DiscountAmount = 0,
                AmountToPay = targetPackage.Price,
                NewStartDate = now,
                NewEndDate = now.AddDays(targetPackage.DurationInDays)
            };
        }

        if (targetType != PackageTypeHelper.Standard)
        {
            return new UpgradePreviewDTO
            {
                CanUpgrade = false,
                Message = "Chính sách nâng cấp bù trừ hiện áp dụng cho gói hội viên tập chung (Standard). Gói PT được đăng ký song song độc lập.",
                CurrentSubscriptionId = currentSameType.Id,
                CurrentPackageName = currentSameType.Package.PackageName,
                CurrentAgreedPrice = currentSameType.AgreedPrice,
                TargetPackageId = targetPackage.Id,
                TargetPackageName = targetPackage.PackageName,
                TargetPackagePrice = targetPackage.Price,
                DiscountAmount = 0,
                AmountToPay = targetPackage.Price,
                NewStartDate = now,
                NewEndDate = now.AddDays(targetPackage.DurationInDays)
            };
        }

        if (targetPackage.Price <= currentSameType.AgreedPrice)
        {
            return new UpgradePreviewDTO
            {
                CanUpgrade = false,
                Message = $"Gói hiện tại ({currentSameType.Package.PackageName}) có giá trị cao hơn hoặc bằng gói muốn chọn. Chỉ có thể nâng cấp lên gói cao hơn.",
                CurrentSubscriptionId = currentSameType.Id,
                CurrentPackageName = currentSameType.Package.PackageName,
                CurrentAgreedPrice = currentSameType.AgreedPrice,
                TargetPackageId = targetPackage.Id,
                TargetPackageName = targetPackage.PackageName,
                TargetPackagePrice = targetPackage.Price,
                DiscountAmount = 0,
                AmountToPay = targetPackage.Price
            };
        }

        var discount = currentSameType.AgreedPrice;
        var amountToPay = targetPackage.Price - discount;
        var newStartDate = currentSameType.StartDate ?? now;
        var newEndDate = newStartDate.AddDays(targetPackage.DurationInDays);

        return new UpgradePreviewDTO
        {
            CanUpgrade = true,
            Message = $"Đủ điều kiện nâng cấp! Bạn chỉ cần thanh toán phần chênh lệch {amountToPay:N0}đ.",
            CurrentSubscriptionId = currentSameType.Id,
            CurrentPackageName = currentSameType.Package.PackageName,
            CurrentAgreedPrice = currentSameType.AgreedPrice,
            TargetPackageId = targetPackage.Id,
            TargetPackageName = targetPackage.PackageName,
            TargetPackagePrice = targetPackage.Price,
            DiscountAmount = discount,
            AmountToPay = amountToPay,
            NewStartDate = newStartDate,
            NewEndDate = newEndDate
        };
    }

    // Xem các gói đã đăng ký của một hội viên.
    public async Task<List<SubscriptionDTO>> GetForMemberAsync(
        int actorId,
        int? memberId,
        CancellationToken cancellationToken = default)
    {
        var member = await ResolveMemberAsync(
            actorId,
            memberId,
            cancellationToken,
            requireActive: false);

        var subscriptions = await ReadSubscriptions()
            .Where(subscription => subscription.MemberId == member.Id)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .ThenByDescending(subscription => subscription.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        return subscriptions
            .Select(subscription => ToDto(subscription, now))
            .ToList();
    }

    // Xem chi tiết đăng ký gói.
    public async Task<SubscriptionDTO> GetByIdAsync(
        int actorId,
        int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            actorId,
            cancellationToken);

        var query = ReadSubscriptions()
            .Where(subscription => subscription.Id == subscriptionId);

        if (!IsStaff(actor))
        {
            if (actor.Role != "Member")
            {
                throw new BusinessException(
                    403,
                    "Bạn không có quyền xem đăng ký gói.");
            }

            query = query.Where(
                subscription => subscription.Member.UserId == actorId);
        }

        var subscription = await query
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy đăng ký gói.");
        }

        return ToDto(subscription, DateTime.UtcNow);
    }

    // Nhân viên xác nhận tiền đã thực sự được nhận tại quầy.
    public async Task<PaymentReceiptDTO> ConfirmPaymentAsync(
        int actorId,
        int subscriptionId,
        ConfirmPaymentRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        return await InTransactionAsync(async () =>
        {
            var actor = await GetActorAsync(
                actorId,
                cancellationToken);

            if (!IsStaff(actor))
            {
                throw new BusinessException(
                    403,
                    "Chỉ lễ tân hoặc quản lý được xác nhận thu tiền.");
            }

            if (!request.PaymentReceived)
            {
                throw new BusinessException(
                    400,
                    "Cần xác nhận đã nhận tiền trước khi lập biên lai.");
            }

            if (request.PaymentMethod != "Cash" &&
                request.PaymentMethod != "BankTransfer" &&
                request.PaymentMethod != "CreditCard")
            {
                throw new BusinessException(
                    400,
                    "Phương thức thanh toán phải là Cash, BankTransfer hoặc CreditCard.");
            }

            if (request.Amount <= 0 ||
                request.Amount > 9999999999999999.99m ||
                decimal.Round(request.Amount, 2) != request.Amount)
            {
                throw new BusinessException(
                    400,
                    "Số tiền phải dương và có tối đa hai chữ số thập phân.");
            }

            var subscription = await _context.MemberSubscriptions
                .Include(subscription => subscription.Member)
                    .ThenInclude(member => member.User)
                .Include(subscription => subscription.Package)
                .SingleOrDefaultAsync(
                    subscription => subscription.Id == subscriptionId,
                    cancellationToken);

            if (subscription == null)
            {
                throw new BusinessException(
                    404,
                    "Không tìm thấy đăng ký gói.");
            }

            if (!subscription.Member.User.IsActive)
            {
                throw new BusinessException(
                    409,
                    "Tài khoản hội viên đã ngừng hoạt động.");
            }

            if (subscription.Status != "Pending")
            {
                throw new BusinessException(
                    409,
                    "Chỉ xác nhận thanh toán cho đăng ký đang Pending.");
            }

            var alreadyPaid = await _context.Payments.AnyAsync(
                payment =>
                    payment.SubscriptionId == subscriptionId &&
                    payment.Status == "Completed",
                cancellationToken);

            if (alreadyPaid)
            {
                throw new BusinessException(
                    409,
                    "Đăng ký gói đã được thanh toán.");
            }

            // Chặn thu thêm tiền khi còn giao dịch chờ hoặc cần đối soát.
            await EnsureNoUnresolvedPaymentAsync(
                subscriptionId,
                cancellationToken);

            if (subscription.AgreedPrice <= 0 ||
                request.Amount != subscription.AgreedPrice)
            {
                throw new BusinessException(
                    400,
                    "Số tiền phải bằng giá đã chốt của đăng ký gói.");
            }

            if (subscription.DurationInDays <= 0 ||
                subscription.DurationInDays > 36500)
            {
                throw new BusinessException(
                    409,
                    "Thời hạn đăng ký gói không hợp lệ.");
            }

            var now = DateTime.UtcNow;

            var payment = new Payment
            {
                MemberId = subscription.MemberId,
                SubscriptionId = subscription.Id,

                Amount = request.Amount,
                AmountReceived = request.Amount,

                PaymentMethod = request.PaymentMethod,
                Status = "Completed",

                CreatedAt = now,
                PaymentDate = now,
                CreatedByUserId = actorId,

                MemberNameSnapshot = subscription.Member.User.FullName,
                PackageNameSnapshot = subscription.Package.PackageName,

                TransactionReference =
                    $"INV-{now:yyyyMMdd}-{Guid.NewGuid():N}"
            };

            _context.Payments.Add(payment);

            // Nếu là gói nâng cấp -> Kế thừa StartDate từ gói cũ và chuyển gói cũ sang Upgraded
            var targetType = PackageTypeHelper.GetPackageType(subscription.Package.PackageName, subscription.Package.Description);

            // Lấy các gói Active về RAM trước (PostgreSQL không dịch được hàm C#)
            var activeSubs = await _context.MemberSubscriptions
                .Include(s => s.Package)
                .Where(s => s.MemberId == subscription.MemberId && s.Id != subscription.Id && s.Status == "Active" && (s.EndDate == null || s.EndDate > now))
                .ToListAsync(cancellationToken);

            // Lọc loại gói trong bộ nhớ
            var oldSub = activeSubs.FirstOrDefault(s =>
                s.Package != null && PackageTypeHelper.GetPackageType(s.Package.PackageName, s.Package.Description) == targetType);

            if (oldSub != null && targetType == PackageTypeHelper.Standard)
            {
                subscription.StartDate = oldSub.StartDate ?? now;
                subscription.EndDate = (oldSub.StartDate ?? now).AddDays(subscription.DurationInDays);
                subscription.Status = "Active";

                oldSub.Status = "Upgraded";
                AddAudit(actorId, "Update", nameof(MemberSubscription), oldSub.Id,
                    $"Gói #{oldSub.Id} chuyển sang Upgraded sau khi nâng cấp lên gói #{subscription.Id}.");
            }
            else
            {
                subscription.StartDate = now;
                subscription.EndDate = now.AddDays(subscription.DurationInDays);
                subscription.Status = "Active";
            }

            await _context.SaveChangesAsync(cancellationToken);

            AddAudit(
                actorId,
                "Create",
                nameof(Payment),
                payment.Id,
                $"Xác nhận thu tiền {payment.PaymentMethod}, biên lai " +
                $"{payment.TransactionReference}, đăng ký gói {subscription.Id}.");

            AddAudit(
                actorId,
                "Update",
                nameof(MemberSubscription),
                subscription.Id,
                "Pending -> Active sau khi xác nhận thanh toán đủ tiền.");

            await _context.SaveChangesAsync(cancellationToken);

            return ToReceipt(payment);
        }, cancellationToken);
    }

    // Hủy đăng ký gói chưa thanh toán.
    public async Task<SubscriptionDTO> CancelPendingAsync(
        int actorId,
        int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        return await InTransactionAsync(async () =>
        {
            var actor = await GetActorAsync(
                actorId,
                cancellationToken);

            var query = _context.MemberSubscriptions
                .Include(subscription => subscription.Package)
                .Include(subscription => subscription.Payments)
                .Where(subscription => subscription.Id == subscriptionId);

            if (!IsStaff(actor))
            {
                if (actor.Role != "Member")
                {
                    throw new BusinessException(
                        403,
                        "Bạn không có quyền hủy đăng ký gói.");
                }

                query = query.Where(
                    subscription => subscription.Member.UserId == actorId);
            }

            var subscription = await query
                .SingleOrDefaultAsync(cancellationToken);

            if (subscription == null)
            {
                throw new BusinessException(
                    404,
                    "Không tìm thấy đăng ký gói.");
            }

            var alreadyPaid = subscription.Payments.Any(
                payment => payment.Status == "Completed");

            if (subscription.Status != "Pending" || alreadyPaid)
            {
                throw new BusinessException(
                    409,
                    "Chỉ hủy đăng ký Pending chưa thanh toán.");
            }

            // Phải xử lý link thanh toán trước khi hủy gói.
            await EnsureNoUnresolvedPaymentAsync(
                subscriptionId,
                cancellationToken);

            subscription.Status = "Cancelled";

            AddAudit(
                actorId,
                "Update",
                nameof(MemberSubscription),
                subscription.Id,
                "Pending -> Cancelled trước khi thanh toán.");

            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(subscription, DateTime.UtcNow);
        }, cancellationToken);
    }

    // Kiểm tra giao dịch còn chờ hoặc cần đối soát.
    private async Task EnsureNoUnresolvedPaymentAsync(
        int subscriptionId,
        CancellationToken ct)
    {
        var payment = await _context.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.SubscriptionId == subscriptionId &&
                (
                    payment.Status == "Pending" ||
                    payment.Status == "NeedsReview"
                ))
            .OrderByDescending(payment => payment.Id)
            .Select(payment => new
            {
                payment.Id,
                payment.Status
            })
            .FirstOrDefaultAsync(ct);

        if (payment == null)
        {
            return;
        }

        if (payment.Status == "NeedsReview")
        {
            throw new BusinessException(
                409,
                $"Giao dịch #{payment.Id} cần đối soát. " +
                "Chưa thể thu thêm tiền hoặc hủy gói.");
        }

        throw new BusinessException(
            409,
            $"Giao dịch #{payment.Id} đang chờ thanh toán. " +
            "Hãy đồng bộ hoặc hủy link payOS thành công trước.");
    }

    private IQueryable<MemberSubscription> ReadSubscriptions()
    {
        return _context.MemberSubscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .Include(subscription => subscription.Payments);
    }

    // Lấy tài khoản đang thực hiện thao tác.
    private async Task<User> GetActorAsync(
        int actorId,
        CancellationToken cancellationToken)
    {
        var actor = await _context.Users
            .SingleOrDefaultAsync(
                user => user.Id == actorId && user.IsActive,
                cancellationToken);

        if (actor == null)
        {
            throw new BusinessException(
                401,
                "Tài khoản không hoạt động. Vui lòng đăng nhập lại.");
        }

        return actor;
    }

    private static bool IsStaff(User user)
    {
        return user.Role == "Manager" ||
               user.Role == "Receptionist";
    }

    // Xác định hội viên: tự thao tác hoặc được nhân viên thao tác hộ.
    private async Task<Member> ResolveMemberAsync(
        int actorId,
        int? memberId,
        CancellationToken cancellationToken,
        bool requireActive = true)
    {
        var actor = await GetActorAsync(
            actorId,
            cancellationToken);

        var query = _context.Members
            .Include(member => member.User)
            .AsQueryable();

        if (memberId.HasValue)
        {
            if (!IsStaff(actor))
            {
                throw new BusinessException(
                    403,
                    "Chỉ lễ tân hoặc quản lý được chọn hội viên khác.");
            }

            query = query.Where(
                member => member.Id == memberId.Value);
        }
        else
        {
            if (actor.Role != "Member")
            {
                throw new BusinessException(
                    403,
                    "Nhân viên cần chọn hội viên cụ thể.");
            }

            query = query.Where(
                member => member.UserId == actorId);
        }

        var member = await query.SingleOrDefaultAsync(
            cancellationToken);

        if (member == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy hồ sơ hội viên.");
        }

        if (requireActive && !member.User.IsActive)
        {
            throw new BusinessException(
                409,
                "Tài khoản hội viên đã ngừng hoạt động.");
        }

        return member;
    }

    // Lưu các thay đổi cùng nhau.
    // Khi có lỗi, transaction chưa commit sẽ được hoàn tác khi dispose.
    private async Task<T> InTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var result = await action();

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException(
                409,
                "Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
        catch (Exception exception) when (
            IsSqlConcurrencyConflict(exception))
        {
            throw new BusinessException(
                409,
                "Có yêu cầu khác đang thay đổi dữ liệu hoặc dữ liệu bị trùng. " +
                "Vui lòng tải lại và thử lại.");
        }
    }

    private static bool IsSqlConcurrencyConflict(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
            if (current is PostgresException pg && pg.SqlState is "23505" or "40001") return true;
        return false;
    }

    private void AddAudit(
        int actorId,
        string action,
        string entityName,
        int entityId,
        string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    // Chuyển đăng ký gói thành dữ liệu trả về API.
    private static SubscriptionDTO ToDto(
        MemberSubscription subscription,
        DateTime now)
    {
        var status = subscription.Status;

        if (status == "Active" &&
            subscription.EndDate.HasValue &&
            subscription.EndDate.Value <= now)
        {
            status = "Expired";
        }

        var payments = subscription.Payments
            .OrderByDescending(payment => payment.PaymentDate)
            .Select(ToReceipt)
            .ToList();

        return new SubscriptionDTO
        {
            Id = subscription.Id,
            MemberId = subscription.MemberId,
            PackageId = subscription.PackageId,
            PackageName = subscription.Package.PackageName,

            AgreedPrice = subscription.AgreedPrice,
            DurationInDays = subscription.DurationInDays,

            Status = status,
            CreatedAt = subscription.CreatedAt,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,

            Payments = payments
        };
    }

    private static PaymentReceiptDTO ToReceipt(Payment payment)
    {
        return new PaymentReceiptDTO
        {
            Id = payment.Id,
            SubscriptionId = payment.SubscriptionId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            PaymentDate = payment.PaymentDate,
            TransactionReference = payment.TransactionReference
        };
    }
}