namespace Sage.Kahoot.Domain.Sessions;

/// <summary>Faixas normativas de live-session. Fonte única para domínio e validators da borda.</summary>
public static class SessionRules
{
    public const int NicknameMinLength = 2;
    public const int NicknameMaxLength = 20;

    public const int PinLength = 6;

    public const int DefaultMaxParticipants = 100;

    /// <summary>Tolerância padrão para latência de rede na janela de resposta.</summary>
    public const int DefaultAnswerToleranceMs = 500;

    /// <summary>Piso da pontuação de um acerto, como fração da pontuação base.</summary>
    public const double MinimumCorrectAnswerFactor = 0.5;
}
