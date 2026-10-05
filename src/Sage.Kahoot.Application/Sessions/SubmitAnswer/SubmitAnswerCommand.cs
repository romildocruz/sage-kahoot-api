using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions.CloseQuestion;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Application.Sessions.SubmitAnswer;

/// <summary>
/// O identificador do participante vem do token, nunca do corpo da requisição.
/// Não há campo de tempo decorrido: o cálculo usa exclusivamente o relógio do servidor.
/// </summary>
public sealed record SubmitAnswerCommand(Guid SessionId, Guid ParticipantId, Guid QuestionId, Guid OptionId)
    : ICommand<Result<AnswerAcceptedResult>>;

public sealed class SubmitAnswerHandler(
    ISessionRepository sessions,
    ISessionNotifier notifier,
    IClock clock,
    IOptions<SessionOptions> options)
    : ICommandHandler<SubmitAnswerCommand, Result<AnswerAcceptedResult>>
{
    public async Task<Result<AnswerAcceptedResult>> ExecuteAsync(SubmitAnswerCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<AnswerAcceptedResult>.Failure(SessionErrors.NotFound);
        }

        if (session.FindParticipant(command.ParticipantId) is null)
        {
            return Result<AnswerAcceptedResult>.Failure(SessionErrors.ParticipantNotInSession);
        }

        var now = clock.UtcNow;
        var window = session.ValidateAnswer(command.QuestionId, command.OptionId, now, options.Value.AnswerToleranceMs);

        if (!window.IsSuccess)
        {
            return Result<AnswerAcceptedResult>.Failure(window.Error!);
        }

        var answer = AnswerRecord.Create(
            session.Id,
            window.Value.Question.Id,
            window.Value.Question.Order,
            command.ParticipantId,
            command.OptionId,
            window.Value.IsCorrect,
            window.Value.ResponseTimeMs,
            window.Value.Points,
            now);

        // O índice único é quem rejeita a segunda resposta: sem leitura prévia, sem janela de corrida.
        if (!await sessions.TryInsertAnswerAsync(answer, ct))
        {
            return Result<AnswerAcceptedResult>.Failure(SessionErrors.AlreadyAnswered);
        }

        await sessions.IncrementParticipantScoreAsync(
            session.Id,
            command.ParticipantId,
            answer.PointsAwarded,
            answer.ResponseTimeMs,
            ct);

        await notifier.AnswerAcceptedAsync(
            command.ParticipantId,
            new AnswerAcceptedPayload(session.Id, answer.QuestionId),
            ct);

        var answeredCount = await sessions.CountAnswersForQuestionAsync(session.Id, answer.QuestionId, ct);

        await notifier.AnswerCountChangedToHostAsync(
            new AnswerCountPayload(session.Id, answer.QuestionId, answeredCount, session.ActiveParticipants.Count),
            ct);

        // Todos os ativos responderam: fecha imediatamente, sem esperar o tempo expirar.
        if (session.ShouldCloseAfterAnswer(answeredCount))
        {
            await new CloseQuestionCommand(session.Id, session.CurrentQuestionIndex).ExecuteAsync(ct);
        }

        return new AnswerAcceptedResult(session.Id, answer.QuestionId, answer.ResponseTimeMs);
    }
}
