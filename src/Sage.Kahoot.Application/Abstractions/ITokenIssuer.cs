namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Token emitido, com o instante de expiração em UTC.</summary>
public sealed record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>
/// Emissão dos dois tipos de token (D6): admin, obtido no login; participante, obtido na entrada por PIN.
/// </summary>
public interface ITokenIssuer
{
    IssuedToken IssueAdminToken();

    IssuedToken IssueParticipantToken(Guid sessionId, Guid participantId, string nickname);
}
