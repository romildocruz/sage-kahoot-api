using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions;

/// <summary>
/// Monta os payloads de tempo real a partir do estado autoritativo e os emite pela porta
/// <see cref="ISessionNotifier"/>. Compartilhado por todos os handlers que fecham ou abrem
/// pergunta — avanço do apresentador, "todos responderam" e expiração do tempo — para que
/// os três caminhos produzam exatamente os mesmos eventos.
/// </summary>
public sealed class SessionBroadcaster(ISessionNotifier notifier, ISessionRepository sessions, IClock clock)
{
    public static QuestionOpenedPayload BuildQuestionOpened(Session session, SessionQuestion question, DateTime nowUtc) =>
        new(session.Id,
            question.Id,
            question.Order,
            session.TotalQuestions,
            question.Text,
            // Sem qualquer indicação da opção correta: ela só é revelada no fechamento.
            [.. question.Options.Select(static o => new PublicOption(o.Id, o.Text))],
            question.TimeLimitSeconds,
            session.CurrentQuestionOpenedAtUtc ?? nowUtc,
            session.CurrentQuestionClosesAtUtc ?? nowUtc,
            session.RemainingSeconds(nowUtc) ?? 0d);

    public static RankingPayload BuildRanking(Session session, bool isFinal)
    {
        var entries = session.Ranking()
            .Select(static (p, index) => new RankingEntry(index + 1, p.Id, p.Nickname, p.TotalScore))
            .ToList();

        return new RankingPayload(session.Id, entries, isFinal);
    }

    public Task QuestionOpenedAsync(Session session, SessionQuestion question, CancellationToken ct) =>
        notifier.QuestionOpenedAsync(BuildQuestionOpened(session, question, clock.UtcNow), ct);

    public Task RankingUpdatedAsync(Session session, bool isFinal, CancellationToken ct) =>
        notifier.RankingUpdatedAsync(BuildRanking(session, isFinal), ct);

    /// <summary>
    /// Revela o resultado da pergunta: distribuição e opção correta ao apresentador,
    /// desempenho individual a cada participante, e o ranking parcial atualizado.
    /// </summary>
    public async Task QuestionClosedAsync(Session session, SessionQuestion question, CancellationToken ct)
    {
        var distribution = await sessions.CountAnswersByOptionAsync(session.Id, question.Id, ct);
        var answers = await sessions.GetAnswersForQuestionAsync(session.Id, question.Id, ct);

        await notifier.QuestionClosedToHostAsync(
            new QuestionClosedHostPayload(
                session.Id,
                question.Id,
                question.CorrectOptionId,
                [.. distribution.Select(static d => new OptionDistribution(d.OptionId, d.Count))]),
            ct);

        var ranking = BuildRanking(session, isFinal: false);
        var answersByParticipant = answers.ToDictionary(static a => a.ParticipantId);

        foreach (var entry in ranking.Entries)
        {
            // Quem não respondeu recebe o mesmo evento, com 0 ponto — a spec exige registrar a ausência.
            var answered = answersByParticipant.TryGetValue(entry.ParticipantId, out var answer);

            await notifier.QuestionResultAsync(
                entry.ParticipantId,
                new QuestionResultPayload(
                    session.Id,
                    question.Id,
                    question.CorrectOptionId,
                    WasCorrect: answered && answer!.IsCorrect,
                    PointsEarned: answered ? answer!.PointsAwarded : 0,
                    TotalScore: entry.TotalScore,
                    Position: entry.Position),
                ct);
        }

        await notifier.RankingUpdatedAsync(ranking, ct);
    }

    public async Task SessionEndedAsync(Session session, CancellationToken ct)
    {
        var ranking = BuildRanking(session, isFinal: true);

        await notifier.SessionEndedToHostAsync(ranking, ct);

        foreach (var entry in ranking.Entries)
        {
            await notifier.SessionEndedAsync(
                entry.ParticipantId,
                new SessionEndedParticipantPayload(session.Id, entry.Position, entry.TotalScore),
                ct);
        }
    }
}
