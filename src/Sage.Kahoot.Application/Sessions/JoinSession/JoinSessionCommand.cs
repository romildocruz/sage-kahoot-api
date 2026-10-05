using System.Text.RegularExpressions;
using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Application.Sessions.JoinSession;

public sealed record JoinSessionCommand(string Pin, string Nickname) : ICommand<Result<JoinSessionResult>>;

public sealed partial class JoinSessionHandler(
    ISessionRepository sessions,
    ITokenIssuer tokenIssuer,
    ISessionNotifier notifier,
    IClock clock,
    IOptions<SessionOptions> options)
    : ICommandHandler<JoinSessionCommand, Result<JoinSessionResult>>
{
    private static readonly DomainError InvalidNickname = DomainError.Validation(
        "session.invalid_nickname",
        $"O apelido deve ter entre {SessionRules.NicknameMinLength} e {SessionRules.NicknameMaxLength} caracteres.");

    public async Task<Result<JoinSessionResult>> ExecuteAsync(JoinSessionCommand command, CancellationToken ct)
    {
        var nickname = Sanitize(command.Nickname);

        if (nickname.Length < SessionRules.NicknameMinLength || nickname.Length > SessionRules.NicknameMaxLength)
        {
            return Result<JoinSessionResult>.Failure(InvalidNickname);
        }

        // PIN só resolve enquanto a sessão está ativa; encerrada, responde como inexistente.
        var session = await sessions.GetActiveByPinAsync(command.Pin, ct);

        if (session is null)
        {
            return Result<JoinSessionResult>.Failure(SessionErrors.PinNotFound);
        }

        var joined = session.Join(nickname, clock.UtcNow, options.Value.MaxParticipants);

        if (!joined.IsSuccess)
        {
            return Result<JoinSessionResult>.Failure(joined.Error!);
        }

        await sessions.ReplaceAsync(session, ct);

        var token = tokenIssuer.IssueParticipantToken(session.Id, joined.Value.Id, nickname);

        await notifier.ParticipantJoinedAsync(
            new ParticipantPresencePayload(session.Id, nickname, session.Participants.Count),
            ct);

        return new JoinSessionResult(
            session.Id,
            joined.Value.Id,
            nickname,
            token.AccessToken,
            token.ExpiresAtUtc,
            session.Quiz.Title,
            session.Status,
            session.TotalQuestions,
            session.Participants.Count);
    }

    /// <summary>
    /// Remove marcação e caracteres de controle do apelido antes de persistir. O apelido vai para
    /// o telão do apresentador, então sanitizar aqui fecha o vetor de XSS na origem — o frontend
    /// não deve ser a única linha de defesa.
    /// </summary>
    private static string Sanitize(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return string.Empty;
        }

        var withoutMarkup = MarkupCharacters().Replace(nickname, string.Empty);
        var withoutControl = ControlCharacters().Replace(withoutMarkup, string.Empty);

        return CollapseWhitespace().Replace(withoutControl, " ").Trim();
    }

    [GeneratedRegex("[<>&\"'`]")]
    private static partial Regex MarkupCharacters();

    [GeneratedRegex(@"\p{C}")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();
}
