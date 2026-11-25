using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SriSakthiLodge_API.Exceptions;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SriSakthiLodge_API.Middleware
{
    /// <summary>
    /// Industrial-standard global exception handler for the API pipeline.
    /// Intercepts all unhandled exceptions across controllers, services, and database layers,
    /// formats them into standardized RFC 7807 ProblemDetails, and logs appropriately via Serilog.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // Client dropped connection / aborted request (e.g. user navigated away or cancelled)
                _logger.LogInformation("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 499; // Client Closed Request
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started, cannot write exception details to response for {Method} {Path}", context.Request.Method, context.Request.Path);
                return;
            }

            var (status, code, message, fieldErrors) = CategorizeException(exception);
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            // Log based on severity: 4xx warnings, 5xx errors with stack traces
            if ((int)status >= 500)
            {
                _logger.LogError(exception, "Unhandled server error [{StatusCode}] on {Method} {Path} (TraceId: {TraceId}): {Message}",
                    (int)status, context.Request.Method, context.Request.Path, traceId, exception.Message);
            }
            else
            {
                _logger.LogWarning("Client error [{StatusCode}:{Code}] on {Method} {Path} (TraceId: {TraceId}): {Message}",
                    (int)status, code, context.Request.Method, context.Request.Path, traceId, message);
            }

            var problem = new ProblemDetailsDto
            {
                Type = $"https://httpstatuses.com/{(int)status}",
                Title = code,
                Status = (int)status,
                Detail = message,
                Instance = context.Request.Path,
                TraceId = traceId,
                Errors = fieldErrors
            };

            // In local development, attach internal exception details for faster debugging
            if (_env.IsDevelopment() && (int)status >= 500)
            {
                problem.Detail = $"{message} | Dev Details: {exception.Message}";
            }

            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/problem+json";

            await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
        }

        private static (HttpStatusCode Status, string Code, string Message, IDictionary<string, string[]>? FieldErrors) CategorizeException(Exception ex)
        {
            return ex switch
            {
                // Custom Domain / Application Exceptions
                ValidationException valEx =>
                    (HttpStatusCode.BadRequest, valEx.Code, valEx.Message, valEx.Errors),

                AppException appEx =>
                    (appEx.StatusCode, appEx.Code, appEx.Message, appEx.Errors),

                // Database Exceptions (MySQL)
                MySqlException sqlEx =>
                    MapMySqlException(sqlEx),

                // Common .NET Framework Exceptions
                UnauthorizedAccessException =>
                    (HttpStatusCode.Unauthorized, "unauthorized", "You are not authorized to access this resource.", null),

                KeyNotFoundException =>
                    (HttpStatusCode.NotFound, "not_found", ex.Message.Length > 0 ? ex.Message : "The requested resource was not found.", null),

                ArgumentNullException argNullEx =>
                    (HttpStatusCode.BadRequest, "validation", $"Missing required field: {argNullEx.ParamName}.",
                        new Dictionary<string, string[]> { { argNullEx.ParamName ?? "field", new[] { "This field is required." } } }),

                ArgumentException argEx =>
                    (HttpStatusCode.BadRequest, "validation", argEx.Message, null),

                BadHttpRequestException badHttpEx =>
                    (HttpStatusCode.BadRequest, "bad_request", badHttpEx.Message, null),

                // Catch-all 500 Internal Server Error
                _ =>
                    (HttpStatusCode.InternalServerError, "server_error", "An unexpected error occurred on the server. Please try again shortly.", null)
            };
        }

        private static (HttpStatusCode Status, string Code, string Message, IDictionary<string, string[]>? FieldErrors) MapMySqlException(MySqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                // 1062: Duplicate entry for key (e.g. unique username, duplicate booking reference)
                1062 => (HttpStatusCode.Conflict, "conflict", "A record with this unique identifier or key already exists.", null),

                // 1451: Cannot delete or update a parent row (Foreign Key constraint fail on delete)
                1451 => (HttpStatusCode.Conflict, "conflict", "This record cannot be deleted because other records depend on it.", null),

                // 1452: Cannot add or update a child row (Foreign Key constraint fail on insert/update)
                1452 => (HttpStatusCode.BadRequest, "bad_request", "The referenced related record does not exist.", null),

                // 1644: ER_SIGNAL_EXCEPTION (explicit SIGNAL SQLSTATE '45000' raised by Stored Procedure)
                1644 => (HttpStatusCode.Conflict, "conflict", sqlEx.Message, null),

                // Connection failures, database server down or connection timeout
                0 or 1042 or 1045 or 2002 or 2003 =>
                    (HttpStatusCode.ServiceUnavailable, "database_unavailable", "The database service is temporarily unavailable. Please retry shortly.", null),

                // Default SQL Error
                _ => (HttpStatusCode.InternalServerError, "database_error", "A database error occurred while processing your request.", null)
            };
        }
    }

    /// <summary>
    /// Standard RFC 7807 ProblemDetails payload representation.
    /// Fully compatible with both Next.js frontend client error normalization and API consumers.
    /// </summary>
    public class ProblemDetailsDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";

        [JsonPropertyName("instance")]
        public string? Instance { get; set; }

        [JsonPropertyName("traceId")]
        public string? TraceId { get; set; }

        [JsonPropertyName("errors")]
        public IDictionary<string, string[]>? Errors { get; set; }
    }
}
