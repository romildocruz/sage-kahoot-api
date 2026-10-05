using System.ComponentModel.DataAnnotations;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Parâmetros operacionais da sessão ao vivo.</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Session";

    [Range(2, 500)]
    public int MaxParticipants { get; set; } = SessionRules.DefaultMaxParticipants;

    /// <summary>Tolerância para latência de rede na janela de resposta.</summary>
    [Range(0, 5_000)]
    public int AnswerToleranceMs { get; set; } = SessionRules.DefaultAnswerToleranceMs;

    /// <summary>Após esse tempo sem atividade, a sessão ativa é cancelada e o PIN liberado.</summary>
    [Range(1, 72)]
    public int AbandonedAfterHours { get; set; } = 4;

    [Range(1, 60)]
    public int AbandonedSweepIntervalMinutes { get; set; } = 10;

    /// <summary>Tentativas de geração de PIN antes de desistir, na colisão com sessões ativas.</summary>
    [Range(1, 50)]
    public int PinGenerationAttempts { get; set; } = 10;
}
