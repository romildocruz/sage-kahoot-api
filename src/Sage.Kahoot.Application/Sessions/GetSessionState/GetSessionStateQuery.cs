using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions.CloseQuestion;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.GetSessionState;

/// <summary>
/// Estado corrente da sessão. É o que um cliente invoca ao reconectar: o servidor devolve
/// a verdade, inclusive o tempo restante recalculado, e o estado do cliente é descartado.
/// </summary>
public sealed record GetSessionStateQuery(Guid SessionId, Guid? ParticipantId)
    : ICommand<Result<SessionStatePayload>>;

public sealed class GetSessionStateHandler(ISessionRepository sessions, IClock clock)
    : ICommandHandler<GetSessionStateQuery, Result<SessionStatePayload>>
{
    public async Task<Result<SessionStatePayload>> ExecuteAsync(GetSessionStateQuery query, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(query.SessionId, ct);

        if (session is null)
        {
            return Result<SessionStatePayload>.Failure(SessionErrors.NotFound);
        }

        // Reavaliação defensiva: a correção não depende só do temporizador do servidor.
        // Se a janela já passou e ninguém fechou, fecha agora antes de responder.
        if (ShouldCloseExpired(session, clock.UtcNow))
        {
            await new CloseQuestionCommand(session.Id, session.CurrentQuestionIndex).ExecuteAsync(ct);
            session = await sessions.GetByIdAsync(query.SessionId, ct) ?? session;
        }

        var now = clock.UtcNow;
        var question = session.CurrentQuestionClosed ? null : session.CurrentQuestion;

        QuestionOpenedPayload? currentQuestion = question is null
            ? null
            : SessionBroadcaster.BuildQuestionOpened(session, question, now);

        int? myScore = null;
        var myAnswerRegistered = false;

        if (query.ParticipantId is { } participantId)
        {
            var participant = session.FindParticipant(participantId);

            if (participant is null)
            {
                return Result<SessionStatePayload>.Failure(SessionErrors.ParticipantNotInSession);
            }

            myScore = participant.TotalScore;

            if (session.CurrentQuestion is { } current)
            {
                var answers = await sessions.GetAnswersForQuestionAsync(session.Id, current.Id, ct);
                myAnswerRegistered = answers.Any(a => a.ParticipantId == participantId);
            }
        }

        return new SessionStatePayload(
            session.Id,
            session.Status.ToString(),
            session.CurrentQuestionIndex,
            session.TotalQuestions,
            currentQuestion,
            session.Participants.Count,
            myScore,
            myAnswerRegistered);
    }

    private static bool ShouldCloseExpired(Session session, DateTime nowUtc) =>
        session.Status is SessionStatus.EmAndamento
        && session.CurrentQuestion is not null
        && !session.CurrentQuestionClosed
        && session.CurrentQuestionClosesAtUtc is { } closesAt
        && nowUtc > closesAt;
}
