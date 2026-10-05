namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Opção como o cliente a vê: sem qualquer indicação de qual é a correta.</summary>
public sealed record PublicOption(Guid Id, string Text);

/// <summary>
/// Abertura de pergunta. Enviado a apresentador e participantes.
/// Não carrega a opção correta — ela só é revelada no fechamento.
/// </summary>
public sealed record QuestionOpenedPayload(
    Guid SessionId,
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Text,
    IReadOnlyList<PublicOption> Options,
    int TimeLimitSeconds,
    DateTime OpenedAtUtc,
    DateTime ClosesAtUtc,
    double RemainingSeconds);

public sealed record ParticipantPresencePayload(Guid SessionId, string Nickname, int TotalParticipants);

public sealed record AnswerCountPayload(Guid SessionId, Guid QuestionId, int AnsweredCount, int ActiveParticipants);

/// <summary>Confirmação de recebimento ao participante — não revela se acertou.</summary>
public sealed record AnswerAcceptedPayload(Guid SessionId, Guid QuestionId);

public sealed record OptionDistribution(Guid OptionId, int Count);

/// <summary>Fechamento da pergunta, visão do apresentador: revela a correta e a distribuição.</summary>
public sealed record QuestionClosedHostPayload(
    Guid SessionId,
    Guid QuestionId,
    Guid CorrectOptionId,
    IReadOnlyList<OptionDistribution> Distribution);

/// <summary>Fechamento da pergunta, visão individual do participante.</summary>
public sealed record QuestionResultPayload(
    Guid SessionId,
    Guid QuestionId,
    Guid CorrectOptionId,
    bool WasCorrect,
    int PointsEarned,
    int TotalScore,
    int Position);

public sealed record RankingEntry(int Position, Guid ParticipantId, string Nickname, int TotalScore);

public sealed record RankingPayload(Guid SessionId, IReadOnlyList<RankingEntry> Entries, bool IsFinal);

public sealed record SessionStartedPayload(Guid SessionId, int TotalQuestions);

public sealed record SessionEndedParticipantPayload(Guid SessionId, int Position, int TotalScore);

/// <summary>
/// Estado corrente devolvido na sincronização de reconexão. O tempo restante é sempre
/// recalculado pelo servidor, nunca deduzido pelo cliente.
/// </summary>
public sealed record SessionStatePayload(
    Guid SessionId,
    string Status,
    int CurrentQuestionIndex,
    int TotalQuestions,
    QuestionOpenedPayload? CurrentQuestion,
    int ParticipantCount,
    int? MyTotalScore,
    bool MyAnswerRegistered);
