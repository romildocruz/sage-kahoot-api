using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Application.Sessions.OpenSession;

public sealed record OpenSessionCommand(Guid QuizId) : ICommand<Result<SessionDto>>;

public sealed class OpenSessionHandler(
    IQuizRepository quizzes,
    ISessionRepository sessions,
    IPinGenerator pinGenerator,
    IClock clock,
    IOptions<SessionOptions> options)
    : ICommandHandler<OpenSessionCommand, Result<SessionDto>>
{
    private static readonly DomainError PinExhausted = DomainError.Conflict(
        "session.pin_generation_failed",
        "Não foi possível gerar um PIN livre. Tente novamente.");

    public async Task<Result<SessionDto>> ExecuteAsync(OpenSessionCommand command, CancellationToken ct)
    {
        var quiz = await quizzes.GetByIdAsync(command.QuizId, ct);

        if (quiz is null)
        {
            return Result<SessionDto>.Failure(QuizErrors.NotFound);
        }

        var pin = await GenerateFreePinAsync(ct);

        if (pin is null)
        {
            return Result<SessionDto>.Failure(PinExhausted);
        }

        // Snapshot: a sessão passa a ser independente do quiz de origem a partir daqui.
        var snapshot = new QuizSnapshot(
            quiz.Id,
            quiz.Title,
            [.. quiz.Questions.Select(static q => new SessionQuestion(
                q.Id,
                q.Order,
                q.Text,
                q.TimeLimitSeconds,
                q.BasePoints,
                [.. q.Options.Select(static o => new SessionAnswerOption(o.Id, o.Text, o.IsCorrect))]))]);

        var session = Session.Open(snapshot, pin, clock.UtcNow);

        if (!session.IsSuccess)
        {
            return Result<SessionDto>.Failure(session.Error!);
        }

        await sessions.InsertAsync(session.Value, ct);

        return SessionMapper.ToDto(session.Value);
    }

    /// <summary>
    /// Sorteia PINs até achar um livre entre as sessões ativas. O índice único parcial no banco
    /// é a garantia final; esta verificação apenas evita o custo de uma inserção que falharia.
    /// </summary>
    private async Task<string?> GenerateFreePinAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < options.Value.PinGenerationAttempts; attempt++)
        {
            var candidate = pinGenerator.Generate();

            if (!await sessions.PinInUseAsync(candidate, ct))
            {
                return candidate;
            }
        }

        return null;
    }
}
