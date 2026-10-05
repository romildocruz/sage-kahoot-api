using System.Security.Claims;
using System.Text;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Sage.Kahoot.Infrastructure.Auth;

/// <summary>
/// Emissor dos dois tokens de D6, ambos assinados com a mesma chave simétrica e
/// distinguidos pela claim de papel.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly AuthOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenIssuer(IOptions<AuthOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken IssueAdminToken()
    {
        var expiresAt = _clock.UtcNow.AddHours(_options.AdminTokenLifetimeHours);

        var claims = new Dictionary<string, object>
        {
            [AuthClaims.Role] = AuthClaims.AdminRole,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        return new IssuedToken(CreateToken(claims, expiresAt), expiresAt);
    }

    public IssuedToken IssueParticipantToken(Guid sessionId, Guid participantId, string nickname)
    {
        var expiresAt = _clock.UtcNow.AddHours(_options.ParticipantTokenLifetimeHours);

        var claims = new Dictionary<string, object>
        {
            [AuthClaims.Role] = AuthClaims.ParticipantRole,
            [AuthClaims.SessionId] = sessionId.ToString(),
            [AuthClaims.ParticipantId] = participantId.ToString(),
            [AuthClaims.Nickname] = nickname,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        return new IssuedToken(CreateToken(claims, expiresAt), expiresAt);
    }

    private string CreateToken(IDictionary<string, object> claims, DateTime expiresAtUtc)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = _clock.UtcNow,
            NotBefore = _clock.UtcNow,
            Expires = expiresAtUtc,
            Claims = claims,
            SigningCredentials = _credentials
        };

        return _handler.CreateToken(descriptor);
    }
}

/// <summary>Leitura tipada das claims do participante a partir do principal autenticado.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? ParticipantId(this ClaimsPrincipal principal) =>
        TryGuid(principal.FindFirst(AuthClaims.ParticipantId)?.Value);

    public static Guid? SessionId(this ClaimsPrincipal principal) =>
        TryGuid(principal.FindFirst(AuthClaims.SessionId)?.Value);

    public static string? Nickname(this ClaimsPrincipal principal) =>
        principal.FindFirst(AuthClaims.Nickname)?.Value;

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirst(AuthClaims.Role)?.Value, AuthClaims.AdminRole, StringComparison.Ordinal);

    private static Guid? TryGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
