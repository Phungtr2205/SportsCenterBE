using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Models;

namespace SportsCenterAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        #region DbSets
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<Coach> Coaches { get; set; } = null!;
        public DbSet<Sport> Sports { get; set; } = null!;
        public DbSet<SportClass> SportClasses { get; set; } = null!;
        public DbSet<MembershipPackage> MembershipPackages { get; set; } = null!;
        public DbSet<MemberSubscription> MemberSubscriptions { get; set; } = null!;
        public DbSet<ClassRegistration> ClassRegistrations { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<TrainingPlan> TrainingPlans { get; set; } = null!;
        public DbSet<Exercise> Exercises { get; set; } = null!;
        public DbSet<TrainingPlanExercise> TrainingPlanExercises { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<ClassSession> ClassSessions { get; set; } = null!;
        public DbSet<CancellationPolicy> CancellationPolicies { get; set; } = null!;
        public DbSet<ClassReview> ClassReviews { get; set; } = null!;
        public DbSet<SupportTicket> SupportTickets { get; set; } = null!;
        public DbSet<GymCheckIn> GymCheckIns { get; set; } = null!;
        public DbSet<Room> Rooms { get; set; } = null!;
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Xóa chữ "dbo" vì Postgres dùng schema mặc định là "public"
            modelBuilder.HasSequence<long>("PayOSOrderNumbers")
                .StartsAt(1)
                .IncrementsBy(1);

            modelBuilder.Entity<Payment>()
                .Property(p => p.AmountReceived)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.PayOSOrderCode)
                .IsUnique()
                .HasFilter("\"PayOSOrderCode\" IS NOT NULL"); // Đã sửa [] thành ""

            #region 1. Unique Indexes
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Member>().HasIndex(m => m.UserId).IsUnique();
            modelBuilder.Entity<Coach>().HasIndex(c => c.UserId).IsUnique();

            modelBuilder.Entity<ClassRegistration>()
                .HasIndex(cr => new { cr.MemberId, cr.ClassId })
                .IsUnique().HasFilter("\"SessionId\" IS NULL"); // Đã sửa [] thành ""

            modelBuilder.Entity<ClassRegistration>()
                .HasIndex(cr => new { cr.MemberId, cr.SessionId })
                .IsUnique().HasFilter("\"SessionId\" IS NOT NULL AND \"Status\" <> 'Cancelled'"); // Đã sửa [] thành "" và bỏ N''

            modelBuilder.Entity<ClassRegistration>().Property(r => r.Status).HasMaxLength(20);
            modelBuilder.Entity<ClassSession>().HasAlternateKey(s => new { s.Id, s.ClassId });

            modelBuilder.Entity<ClassRegistration>().HasOne(r => r.Session).WithMany(s => s.Registrations)
                .HasForeignKey(r => new { r.SessionId, r.ClassId }).HasPrincipalKey(s => new { s.Id, s.ClassId });

            modelBuilder.Entity<Attendance>().HasOne(a => a.Session).WithMany(s => s.Attendances)
                .HasForeignKey(a => new { a.SessionId, a.ClassId }).HasPrincipalKey(s => new { s.Id, s.ClassId });

            modelBuilder.Entity<Attendance>().HasIndex(a => new { a.MemberId, a.SessionId })
                .IsUnique().HasFilter("\"SessionId\" IS NOT NULL"); // Đã sửa [] thành ""

            modelBuilder.Entity<ClassRegistration>().HasOne(r => r.CreatedByUser).WithMany().HasForeignKey(r => r.CreatedByUserId);
            modelBuilder.Entity<ClassRegistration>().HasOne(r => r.CancelledByUser).WithMany().HasForeignKey(r => r.CancelledByUserId);

            modelBuilder.Entity<ClassReview>().HasIndex(r => r.RegistrationId).IsUnique();
            modelBuilder.Entity<ClassReview>().ToTable(t => t.HasCheckConstraint("CK_ClassReviews_Rating", "\"Rating\" BETWEEN 1 AND 5")); // Đã sửa [] thành ""

            modelBuilder.Entity<ClassSession>().HasIndex(s => new { s.CoachId, s.StartsAt, s.EndsAt });
            modelBuilder.Entity<ClassSession>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_ClassSessions_Time", "\"EndsAt\" > \"StartsAt\""); // Đã sửa [] thành ""
                t.HasCheckConstraint("CK_ClassSessions_Capacity", "\"Capacity\" > 0"); // Đã sửa [] thành ""
            });

            modelBuilder.Entity<CancellationPolicy>().ToTable(t => t.HasCheckConstraint("CK_CancellationPolicies_Hours", "\"MinimumHoursBeforeStart\" BETWEEN 2 AND 720")); // Đã sửa [] thành ""
            modelBuilder.Entity<CancellationPolicy>().HasData(new CancellationPolicy
            {
                Id = 1,
                Name = "Hủy trước ít nhất 2 tiếng",
                MinimumHoursBeforeStart = 2,
                IsActive = true
            });

            modelBuilder.Entity<MemberSubscription>()
                .HasIndex(subscription => new { subscription.MemberId, subscription.PackageId, subscription.Status });
            modelBuilder.Entity<MemberSubscription>()
                .Property(subscription => subscription.Status).HasMaxLength(20);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.SubscriptionId).IsUnique()
                .HasFilter("\"SubscriptionId\" IS NOT NULL AND \"Status\" = 'Completed'"); // Đã sửa [] thành "" và bỏ N''

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.TransactionReference).HasMaxLength(100);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.TransactionReference).IsUnique()
                .HasFilter("\"TransactionReference\" IS NOT NULL"); // Đã sửa [] thành ""

            modelBuilder.Entity<SupportTicket>()
                .HasOne(t => t.Member).WithMany().HasForeignKey(t => t.MemberId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SupportTicket>()
                .HasOne(t => t.HandledByUser).WithMany().HasForeignKey(t => t.HandledByUserId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GymCheckIn>()
                .HasOne(g => g.Member)
                .WithMany()
                .HasForeignKey(g => g.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GymCheckIn>()
                .HasOne(g => g.CheckedByUser)
                .WithMany()
                .HasForeignKey(g => g.CheckedInByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClassSession>()
                .HasOne(cs => cs.Room)
                .WithMany(r => r.ClassSessions)
                .HasForeignKey(cs => cs.RoomId)
                .OnDelete(DeleteBehavior.SetNull);
            #endregion

            #region 2. Decimal Precision Configurations
            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<MembershipPackage>().Property(mp => mp.Price).HasPrecision(18, 2);
            modelBuilder.Entity<MemberSubscription>().Property(subscription => subscription.AgreedPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SportClass>().Property(sc => sc.Price).HasPrecision(18, 2);
            #endregion

            #region 3. Cascade Delete Behavior
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
            #endregion

            #region 4. Data Seeding
            const string defaultAdminPasswordHash = "$2a$11$yyFr5UfBjj8AJQZXXDiuUuNkYvAYEwcdV7EYeGOdE4CsHPDimmLt6";
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                FullName = "System Administrator",
                Email = "admin@sportscenter.com",
                PasswordHash = defaultAdminPasswordHash,
                Role = "Manager",
                PhoneNumber = "0901234567",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            #endregion
        }
    }
}