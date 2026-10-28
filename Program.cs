using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.Middleware;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// === Add Services ===

// Controllers
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Swagger / OpenAPI Documentation
builder.Services.AddSwaggerGen(option =>
{
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    option.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    option.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Sports Center Management System API",
        Version = "v1",
        Description = "API for managing sports center operations, including user accounts, memberships, and classes."
    });

    // 1. Định nghĩa cách nhập Token
    option.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Nhập trực tiếp JWT Token vào ô dưới đây (không cần gõ 'Bearer ')"
    });

    // 2. Yêu cầu Swagger UI phải tự động gắn Token vào Request Header
    option.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

// OpenAPI/Scalar
//builder.Services.AddOpenApi(options =>
//{
//    options.AddDocumentTransformer((document, context, cancellationToken) =>
//    {
//        document.Components ??= new();
//        document.Components.SecuritySchemes ??=
//            new Dictionary<string, IOpenApiSecurityScheme>();

//        document.Components.SecuritySchemes["Bearer"] =
//            new OpenApiSecurityScheme
//            {
//                Type = SecuritySchemeType.Http,
//                Scheme = "bearer",
//                BearerFormat = "JWT",
//                Description = "Enter JWT Bearer Token."
//            };

//        return Task.CompletedTask;
//    });
//    options.AddOperationTransformer((operation, context, cancellationToken) =>
//    {
//        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
//        if (metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any() &&
//            !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
//        {
//            operation.Security = [new OpenApiSecurityRequirement
//            {
//                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
//            }];
//        }
//        return Task.CompletedTask;
//    });
//});

// Entity Framework Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };
        // Role changes and soft deletion invalidate previously issued tokens immediately.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                {
                    context.Fail("Invalid account identifier.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var account = await db.Users.AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new { user.IsActive, user.Role })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (account is null || !account.IsActive || account.Role != context.Principal?.FindFirstValue(ClaimTypes.Role))
                    context.Fail("Account is inactive or permissions have changed. Please sign in again.");
            }
        };
    });
builder.Services.AddAuthorization();

// CORS (cho Frontend gọi API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Node.js frontend port
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register Services (DI)

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.Configure<MailSettings>(
builder.Configuration.GetSection("MailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<MemoryOtpStore>();
builder.Services.AddHttpClient<IAIService, AIService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IClassCatalogService, ClassCatalogService>();
builder.Services.AddScoped<ITrainingPlanService, TrainingPlanService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IExerciseService, ExerciseService>();


// Đọc cấu hình từ mục "PayOS" trong appsettings.json.
builder.Services.Configure<PayOSConfig>(
builder.Configuration.GetSection("PayOS"));

// Gọi API của payOS.
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

// Attendance Service
builder.Services.AddScoped<IAttendanceService, AttendanceService>();

//Support Ticket Service
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();

// Gym Check-In Service
builder.Services.AddScoped<IGymCheckInService, GymCheckInService>();

// Room Service
builder.Services.AddScoped<IRoomService, RoomService>();

// Exercise Service

var app = builder.Build();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// === Configure Middleware Pipeline ===

// Global exception handler
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    //bật giao diện Swagger UI khi chạy ứng dụng
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sports Center Management System API v1");
        c.RoutePrefix = "swagger"; // đường dẫn để truy cập Swagger UI, ví dụ: http://localhost:5000/swagger
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

//// Auto-migrate database on startup (development only)
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    db.Database.Migrate();
//}

// Tự động chạy Migration và nạp dữ liệu mẫu
if (builder.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SportsCenterAPI.Data.AppDbContext>();
    db.Database.Migrate();
    await SportsCenterAPI.Data.DbSeeder.SeedAsync(db);
}


app.Run();
