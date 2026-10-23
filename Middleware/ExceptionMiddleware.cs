using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Helpers;

namespace SportsCenterAPI.Middleware
{
    /// <summary>
    /// Global exception handling middleware to catch unhandled exceptions,
    /// log detailed error information, and return a standardized JSON error response.
    /// 
    /// Middleware xử lý ngoại lệ toàn cục cho ứng dụng ASP.NET Core:
    /// Bắt tất cả các lỗi chưa được xử lý, ghi log chi tiết và trả về JSON chuẩn hóa { statusCode, message }.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes middleware logic for the HTTP request.
        /// Thực thi middleware trong chu trình xử lý yêu cầu HTTP.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // Client đã ngắt kết nối, không ghi lỗi 500 hoặc cố gửi phản hồi.
            }
            catch (Exception ex)
            {
                // Log exception details with request path and method
                // Ghi log chi tiết ngoại lệ kèm theo đường dẫn và phương thức HTTP
                _logger.LogError(ex, "An unhandled exception occurred while processing request {Method} {Path}: {ErrorMessage}",
                    context.Request.Method,
                    context.Request.Path,
                    ex.Message);

                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Formats and writes the standardized JSON error response.
        /// Định dạng và gửi phản hồi lỗi dạng JSON chuẩn hóa về client.
        /// </summary>
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // Map common exception types to appropriate HTTP status codes
            // Phân loại mã trạng thái HTTP dựa trên kiểu ngoại lệ
            var statusCode = exception switch
            {
                BusinessException business => business.StatusCode,
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => StatusCodes.Status409Conflict,
                KeyNotFoundException => (int)HttpStatusCode.NotFound, // 404
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized, // 401
                ArgumentNullException or ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest, // 400
                _ => (int)HttpStatusCode.InternalServerError // 500
            };

            context.Response.StatusCode = statusCode;

            // Prepare standardized error response object: { statusCode, message }
            // Chuẩn bị đối tượng lỗi chuẩn hóa theo yêu cầu: { statusCode, message }
            var errorResponse = new ErrorResponse
            {
                StatusCode = statusCode,
                Message = exception is DbUpdateException && statusCode == StatusCodes.Status409Conflict
                    ? "The record already exists or was changed by another request. Reload and try again."
                    : statusCode == (int)HttpStatusCode.InternalServerError
                    ? "An unexpected internal server error occurred. Please try again later."
                    : exception.Message
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var jsonResult = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(jsonResult);
        }
    }

    /// <summary>
    /// Standardized error response model.
    /// Model đại diện cho phản hồi lỗi chuẩn hóa: { statusCode, message }.
    /// </summary>
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Extension method to easily register ExceptionMiddleware in Program.cs.
    /// Phương thức mở rộng giúp đăng ký ExceptionMiddleware vào pipeline ứng dụng một cách thuận tiện.
    /// </summary>
    public static class ExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Adds the global ExceptionMiddleware to the application request pipeline.
        /// Thêm ExceptionMiddleware vào pipeline xử lý request.
        /// </summary>
        public static Microsoft.AspNetCore.Builder.IApplicationBuilder UseGlobalExceptionMiddleware(
            this Microsoft.AspNetCore.Builder.IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
