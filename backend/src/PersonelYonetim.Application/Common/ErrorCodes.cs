namespace PersonelYonetim.Application.Common;

/// <summary>
/// Makine-okunur hata kodları.
/// UI mesajı ayrıdır; kod sabittir (i18n / log / izleme için).
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string BusinessRule = "BUSINESS_RULE_VIOLATION";
    public const string Unexpected = "UNEXPECTED_ERROR";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
}
