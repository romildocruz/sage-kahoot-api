using System.Security.Cryptography;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Infrastructure.Sessions;

/// <summary>
/// PIN numérico de 6 dígitos a partir de fonte criptograficamente segura.
/// Gerar com RNG (e não com Random) evita PINs previsíveis a partir do horário de abertura da sessão.
/// </summary>
public sealed class SecurePinGenerator : IPinGenerator
{
    private const int UpperBoundExclusive = 1_000_000;

    public string Generate() =>
        RandomNumberGenerator.GetInt32(UpperBoundExclusive)
            .ToString($"D{SessionRules.PinLength}", System.Globalization.CultureInfo.InvariantCulture);
}
