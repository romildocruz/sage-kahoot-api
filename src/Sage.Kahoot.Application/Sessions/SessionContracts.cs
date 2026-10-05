using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions;

/// <summary>Sessão recém-aberta, como o apresentador precisa vê-la.</summary>
public sealed record SessionDto(
    Guid Id,
    string Pin,
    Guid QuizId,
    string QuizTitle,
    SessionStatus Status,
    int TotalQuestions,
    int ParticipantCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Retorno da entrada por PIN. O token vincula o participante àquela sessão e é o que
/// permite reconectar mantendo apelido e pontuação.
/// </summary>
public sealed record JoinSessionResult(
    Guid SessionId,
    Guid ParticipantId,
    string Nickname,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string QuizTitle,
    SessionStatus Status,
    int TotalQuestions,
    int ParticipantCount);

/// <summary>
/// Confirmação de recebimento da resposta. Não informa acerto nem pontos:
/// isso só é revelado no fechamento da pergunta.
/// </summary>
public sealed record AnswerAcceptedResult(Guid SessionId, Guid QuestionId, long ResponseTimeMs);

public static class SessionMapper
{
    public static SessionDto ToDto(Session session) =>
        new(session.Id,
            session.Pin,
            session.Quiz.QuizId,
            session.Quiz.Title,
            session.Status,
            session.TotalQuestions,
            session.Participants.Count,
            session.CreatedAtUtc);
}
