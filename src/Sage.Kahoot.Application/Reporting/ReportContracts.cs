using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Reporting;

public sealed record ParticipantReport(
    int Position,
    Guid ParticipantId,
    string Nickname,
    int TotalScore,
    int CorrectCount,
    int WrongCount,
    int UnansweredCount,
    double AverageResponseTimeMs)
{
    /// <summary>As três primeiras posições, para destacar o pódio na tela de resultado.</summary>
    public bool IsPodium => Position <= 3;
}

public sealed record QuestionOptionReport(Guid OptionId, string Text, bool IsCorrect, int AnswerCount);

public sealed record QuestionReport(
    Guid QuestionId,
    int Order,
    string Text,
    Guid CorrectOptionId,
    IReadOnlyList<QuestionOptionReport> Options,
    int AnswerCount,
    int UnansweredCount,
    double CorrectRate,
    double AverageResponseTimeMs);

public sealed record SessionReport(
    Guid SessionId,
    string Pin,
    Guid QuizId,
    string QuizTitle,
    SessionStatus Status,
    bool IsFinal,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? EndedAtUtc,
    int ParticipantCount,
    IReadOnlyList<ParticipantReport> Ranking,
    IReadOnlyList<QuestionReport> Questions);
