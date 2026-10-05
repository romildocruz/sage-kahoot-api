namespace Sage.Kahoot.Domain.Common;

/// <summary>
/// Categoria do erro de domínio. Determina o status HTTP na borda (ver a extensão ToHttp na camada Api).
/// </summary>
public enum ErrorCategory
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}

/// <summary>
/// Falha previsível de regra de negócio. Não é exceção: é valor retornado por handlers via <see cref="Result{T}"/>.
/// Exceções ficam reservadas a falhas genuinamente inesperadas.
/// </summary>
public sealed record DomainError(string Code, string Message, ErrorCategory Category)
{
    public static DomainError Validation(string code, string message) => new(code, message, ErrorCategory.Validation);

    public static DomainError NotFound(string code, string message) => new(code, message, ErrorCategory.NotFound);

    public static DomainError Conflict(string code, string message) => new(code, message, ErrorCategory.Conflict);

    public static DomainError Unauthorized(string code, string message) => new(code, message, ErrorCategory.Unauthorized);
}
