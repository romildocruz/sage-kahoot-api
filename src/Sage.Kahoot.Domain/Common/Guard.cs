namespace Sage.Kahoot.Domain.Common;

/// <summary>
/// Verificações de invariante do domínio. Retornam <see cref="DomainError"/> em vez de lançar,
/// para compor com <see cref="Result{T}"/> sem usar exceção como fluxo de controle.
/// </summary>
public static class Guard
{
    public static DomainError? AgainstNullOrWhiteSpace(string? value, string code, string field) =>
        string.IsNullOrWhiteSpace(value) ? DomainError.Validation(code, $"{field} é obrigatório.") : null;

    public static DomainError? AgainstLengthOutOfRange(string value, int min, int max, string code, string field) =>
        value.Length < min || value.Length > max
            ? DomainError.Validation(code, $"{field} deve ter entre {min} e {max} caracteres.")
            : null;

    public static DomainError? AgainstOutOfRange(int value, int min, int max, string code, string field) =>
        value < min || value > max
            ? DomainError.Validation(code, $"{field} deve estar entre {min} e {max}.")
            : null;

    public static DomainError? AgainstCountOutOfRange<T>(IReadOnlyCollection<T> items, int min, int max, string code, string field) =>
        items.Count < min || items.Count > max
            ? DomainError.Validation(code, $"{field} deve conter entre {min} e {max} itens.")
            : null;

    /// <summary>Retorna o primeiro erro não nulo, ou null se todas as verificações passaram.</summary>
    public static DomainError? FirstFailure(params DomainError?[] checks) =>
        Array.Find(checks, static c => c is not null);
}
