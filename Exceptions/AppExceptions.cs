using System.Net;

namespace SriSakthiLodge_API.Exceptions
{
    /// <summary>
    /// Base application exception carrying HTTP status and code for RFC 7807 problem details.
    /// </summary>
    public class AppException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Code { get; }
        public IDictionary<string, string[]>? Errors { get; }

        public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string code = "bad_request", IDictionary<string, string[]>? errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            Errors = errors;
        }
    }

    /// <summary>
    /// Thrown when a requested resource does not exist (HTTP 404).
    /// </summary>
    public class NotFoundException : AppException
    {
        public NotFoundException(string message = "The requested resource was not found.")
            : base(message, HttpStatusCode.NotFound, "not_found")
        {
        }
    }

    /// <summary>
    /// Thrown when a state conflict occurs, e.g. overlapping booking, duplicate active session (HTTP 409).
    /// </summary>
    public class ConflictException : AppException
    {
        public ConflictException(string message = "The request conflicts with the current resource state.")
            : base(message, HttpStatusCode.Conflict, "conflict")
        {
        }
    }

    /// <summary>
    /// Thrown when request arguments or parameters are invalid (HTTP 400).
    /// </summary>
    public class BadRequestException : AppException
    {
        public BadRequestException(string message = "Invalid request parameters.")
            : base(message, HttpStatusCode.BadRequest, "bad_request")
        {
        }
    }

    /// <summary>
    /// Thrown when an authenticated user does not have required permissions (HTTP 403).
    /// </summary>
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "You do not have permission to perform this action.")
            : base(message, HttpStatusCode.Forbidden, "forbidden")
        {
        }
    }

    /// <summary>
    /// Thrown when authentication is missing or token is invalid/expired (HTTP 401).
    /// </summary>
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message = "Authentication is required or session has expired.")
            : base(message, HttpStatusCode.Unauthorized, "unauthorized")
        {
        }
    }

    /// <summary>
    /// Thrown when model or domain validation fails with field-specific errors (HTTP 400).
    /// </summary>
    public class ValidationException : AppException
    {
        public ValidationException(string message, IDictionary<string, string[]> errors)
            : base(message, HttpStatusCode.BadRequest, "validation", errors)
        {
        }

        public ValidationException(string field, string error)
            : base("Validation failed.", HttpStatusCode.BadRequest, "validation", new Dictionary<string, string[]>
            {
                { field, new[] { error } }
            })
        {
        }
    }
}

