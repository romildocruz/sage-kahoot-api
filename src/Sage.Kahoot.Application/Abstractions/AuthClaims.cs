namespace Sage.Kahoot.Application.Abstractions;

/// <summary>
/// Nomes de claim e papéis compartilhados entre o emissor do token (Infrastructure)
/// e as políticas de autorização (Api).
/// </summary>
public static class AuthClaims
{
    public const string Role = "role";
    public const string SessionId = "sessionId";
    public const string ParticipantId = "participantId";
    public const string Nickname = "nickname";

    public const string AdminRole = "admin";
    public const string ParticipantRole = "participant";

    public const string AdminPolicy = "AdminOnly";
    public const string ParticipantPolicy = "SessionParticipant";
}
