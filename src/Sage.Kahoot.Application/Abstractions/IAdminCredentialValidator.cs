namespace Sage.Kahoot.Application.Abstractions;

/// <summary>
/// Porta de validação da credencial única do admin. A comparação em tempo constante é
/// responsabilidade da implementação, na camada de infraestrutura.
/// </summary>
public interface IAdminCredentialValidator
{
    bool Validate(string? username, string? password);
}
