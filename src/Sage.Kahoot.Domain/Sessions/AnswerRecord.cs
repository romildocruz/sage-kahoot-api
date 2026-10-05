namespace Sage.Kahoot.Domain.Sessions;

/// <summary>
/// Resposta registrada. É a fonte de verdade da sessão: a pontuação acumulada no participante
/// é cache reconstruível a partir daqui.
/// </summary>
public sealed record AnswerRecord(
    Guid Id,
    Guid SessionId,
    Guid QuestionId,
    int QuestionIndex,
    Guid ParticipantId,
    Guid SelectedOptionId,
    bool IsCorrect,
    long ResponseTimeMs,
    int PointsAwarded,
    DateTime AnsweredAtUtc)
{
    public static AnswerRecord Create(
        Guid sessionId,
        Guid questionId,
        int questionIndex,
        Guid participantId,
        Guid selectedOptionId,
        bool isCorrect,
        long responseTimeMs,
        int pointsAwarded,
        DateTime answeredAtUtc) =>
        new(Guid.NewGuid(), sessionId, questionId, questionIndex, participantId, selectedOptionId,
            isCorrect, responseTimeMs, pointsAwarded, answeredAtUtc);
}
