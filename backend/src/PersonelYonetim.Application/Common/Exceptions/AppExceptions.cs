namespace PersonelYonetim.Application.Common.Exceptions;

public class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(ErrorCodes.NotFound, message, StatusCodes.Status404NotFound)
    {
    }

    // AspNetCore StatusCodes bu katmanda yok; sabit kullan.
    private static class StatusCodes
    {
        public const int Status404NotFound = 404;
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bu işlem için yetkiniz yok.")
        : base(ErrorCodes.Forbidden, message, 403)
    {
    }
}

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string code, string message)
        : base(code, message, 401)
    {
    }
}

public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.Validation, "Doğrulama hatası.", 400)
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(ErrorCodes.Conflict, message, 409)
    {
    }
}
