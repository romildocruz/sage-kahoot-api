using System.Security.Cryptography;
using System.Text;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Infrastructure.Auth;

/// <summary>
/// Compara a credencial informada com a única configurada por Secret.
/// </summary>
public sealed class AdminCredentialValidator(IOptions<AuthOptions> options) : IAdminCredentialValidator
{
    private readonly AuthOptions _options = options.Value;

    /// <summary>
    /// Compara usuário e senha em tempo constante. Ambas as comparações são sempre executadas,
    /// e o resultado é combinado com AND não curto-circuitado, para que o tempo de resposta
    /// não revele qual dos dois campos estava errado.
    /// </summary>
    public bool Validate(string? username, string? password)
    {
        var usernameMatches = FixedTimeEquals(username, _options.AdminUsername);
        var passwordMatches = FixedTimeEquals(password, _options.AdminPassword);

        return usernameMatches & passwordMatches;
    }

    private static bool FixedTimeEquals(string? candidate, string expected)
    {
        var candidateBytes = Encoding.UTF8.GetBytes(candidate ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // FixedTimeEquals exige mesmo comprimento; comparar os hashes mantém o tempo constante
        // mesmo quando os comprimentos diferem, sem vazar o tamanho da senha configurada.
        Span<byte> candidateHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];

        SHA256.HashData(candidateBytes, candidateHash);
        SHA256.HashData(expectedBytes, expectedHash);

        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }
}
