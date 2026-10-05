using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Quizzes;
using Sage.Kahoot.Domain.Sessions;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.UnitTests;

/// <summary>Relógio controlado: a pontuação depende do tempo, então o teste precisa mandar nele.</summary>
public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

public sealed class FakePinGenerator(string pin = "123456") : IPinGenerator
{
    public string Generate() => pin;
}

public sealed class FakeTokenIssuer : ITokenIssuer
{
    public IssuedToken IssueAdminToken() =>
        new("token-admin-de-teste", DateTime.UtcNow.AddHours(8));

    public IssuedToken IssueParticipantToken(Guid sessionId, Guid participantId, string nickname) =>
        new($"token-{participantId}", DateTime.UtcNow.AddHours(6));
}

/// <summary>Registra os eventos emitidos para que o teste verifique o que foi anunciado.</summary>
public sealed class RecordingNotifier : ISessionNotifier
{
    public List<object> Events { get; } = [];

    public Task ParticipantJoinedAsync(ParticipantPresencePayload p, CancellationToken ct) => Record(p);

    public Task ParticipantLeftAsync(ParticipantPresencePayload p, CancellationToken ct) => Record(p);

    public Task SessionStartedAsync(SessionStartedPayload p, CancellationToken ct) => Record(p);

    public Task QuestionOpenedAsync(QuestionOpenedPayload p, CancellationToken ct) => Record(p);

    public Task AnswerCountChangedToHostAsync(AnswerCountPayload p, CancellationToken ct) => Record(p);

    public Task AnswerAcceptedAsync(Guid participantId, AnswerAcceptedPayload p, CancellationToken ct) => Record(p);

    public Task QuestionClosedToHostAsync(QuestionClosedHostPayload p, CancellationToken ct) => Record(p);

    public Task QuestionResultAsync(Guid participantId, QuestionResultPayload p, CancellationToken ct) => Record(p);

    public Task RankingUpdatedAsync(RankingPayload p, CancellationToken ct) => Record(p);

    public Task SessionEndedToHostAsync(RankingPayload p, CancellationToken ct) => Record(p);

    public Task SessionEndedAsync(Guid participantId, SessionEndedParticipantPayload p, CancellationToken ct) => Record(p);

    private Task Record(object payload)
    {
        Events.Add(payload);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryQuizRepository : IQuizRepository
{
    public Dictionary<Guid, Quiz> Items { get; } = [];

    public Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public Task<PagedResult<QuizListItem>> ListAsync(ListQuizzesFilter filter, CancellationToken ct)
    {
        var items = Items.Values
            .Where(q => string.IsNullOrWhiteSpace(filter.TitleContains)
                || q.Title.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(q => q.UpdatedAtUtc)
            .Select(q => new QuizListItem(q.Id, q.Title, q.Description, q.Questions.Count, q.CreatedAtUtc, q.UpdatedAtUtc))
            .ToList();

        return Task.FromResult(new PagedResult<QuizListItem>(items, items.Count, filter.Page, filter.PageSize));
    }

    public Task InsertAsync(Quiz quiz, CancellationToken ct)
    {
        Items[quiz.Id] = quiz;
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(Quiz quiz, CancellationToken ct)
    {
        Items[quiz.Id] = quiz;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.Remove(id));

    public Task<bool> SeedQuizExistsAsync(CancellationToken ct) =>
        Task.FromResult(Items.Values.Any(q => q.IsSeed));
}

/// <summary>
/// Reproduz as garantias que em produção vêm do banco: unicidade de resposta por
/// participante/pergunta e fechamento único da pergunta.
/// </summary>
public sealed class InMemorySessionRepository : ISessionRepository
{
    public Dictionary<Guid, Session> Sessions { get; } = [];

    public List<AnswerRecord> Answers { get; } = [];

    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Sessions.GetValueOrDefault(id));

    public Task<Session?> GetActiveByPinAsync(string pin, CancellationToken ct) =>
        Task.FromResult(Sessions.Values.FirstOrDefault(s => s.Pin == pin && s.Status.IsActive()));

    public Task InsertAsync(Session session, CancellationToken ct)
    {
        Sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(Session session, CancellationToken ct)
    {
        Sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceIfCurrentQuestionAsync(Session session, int expectedQuestionIndex, CancellationToken ct)
    {
        Sessions[session.Id] = session;
        return Task.FromResult(true);
    }

    public Task<bool> TryCloseCurrentQuestionAsync(Guid sessionId, int questionIndex, DateTime closedAtUtc, CancellationToken ct)
    {
        var session = Sessions.GetValueOrDefault(sessionId);

        if (session is null || session.CurrentQuestionIndex != questionIndex || session.CurrentQuestionClosed)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(session.CloseCurrentQuestionOnly(closedAtUtc).IsSuccess);
    }

    public Task<bool> HasActiveSessionForQuizAsync(Guid quizId, CancellationToken ct) =>
        Task.FromResult(Sessions.Values.Any(s => s.Quiz.QuizId == quizId && s.Status.IsActive()));

    public Task<bool> PinInUseAsync(string pin, CancellationToken ct) =>
        Task.FromResult(Sessions.Values.Any(s => s.Pin == pin && s.Status.IsActive()));

    public Task<bool> TryInsertAnswerAsync(AnswerRecord answer, CancellationToken ct)
    {
        var duplicate = Answers.Any(a =>
            a.SessionId == answer.SessionId
            && a.ParticipantId == answer.ParticipantId
            && a.QuestionId == answer.QuestionId);

        if (duplicate)
        {
            return Task.FromResult(false);
        }

        Answers.Add(answer);
        return Task.FromResult(true);
    }

    public Task IncrementParticipantScoreAsync(Guid sessionId, Guid participantId, int points, long responseTimeMs, CancellationToken ct)
    {
        Sessions.GetValueOrDefault(sessionId)?.FindParticipant(participantId)?.AddScore(points, responseTimeMs);
        return Task.CompletedTask;
    }

    public Task SetParticipantConnectedAsync(Guid sessionId, Guid participantId, bool isConnected, DateTime nowUtc, CancellationToken ct)
    {
        Sessions.GetValueOrDefault(sessionId)?.FindParticipant(participantId)?.SetConnected(isConnected);
        return Task.CompletedTask;
    }

    public Task<int> CountAnswersForQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct) =>
        Task.FromResult(Answers.Count(a => a.SessionId == sessionId && a.QuestionId == questionId));

    public Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(Guid sessionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AnswerRecord>>([.. Answers.Where(a => a.SessionId == sessionId)]);

    public Task<IReadOnlyList<AnswerRecord>> GetAnswersForQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AnswerRecord>>(
            [.. Answers.Where(a => a.SessionId == sessionId && a.QuestionId == questionId)]);

    public Task<IReadOnlyList<OptionAnswerCount>> CountAnswersByOptionAsync(Guid sessionId, Guid questionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OptionAnswerCount>>(
            [.. Answers
                .Where(a => a.SessionId == sessionId && a.QuestionId == questionId)
                .GroupBy(a => a.SelectedOptionId)
                .Select(g => new OptionAnswerCount(g.Key, g.Count()))]);

    public Task<PagedResult<SessionListItem>> ListAsync(ListSessionsFilter filter, CancellationToken ct)
    {
        var items = Sessions.Values
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SessionListItem(
                s.Id, s.Pin, s.Quiz.QuizId, s.Quiz.Title, s.Status, s.CreatedAtUtc, s.StartedAtUtc, s.EndedAtUtc,
                s.Participants.Count,
                s.Participants.Count == 0 ? 0 : s.Participants.Max(p => p.TotalScore)))
            .ToList();

        return Task.FromResult(new PagedResult<SessionListItem>(items, items.Count, filter.Page, filter.PageSize));
    }

    public Task<IReadOnlyList<Session>> GetWithExpiredQuestionAsync(DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Session>>(
            [.. Sessions.Values.Where(s =>
                s.Status == SessionStatus.EmAndamento
                && !s.CurrentQuestionClosed
                && s.CurrentQuestionClosesAtUtc < nowUtc)]);

    public Task<IReadOnlyList<Session>> GetAbandonedAsync(DateTime inactiveSinceUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Session>>(
            [.. Sessions.Values.Where(s => s.Status.IsActive() && s.LastActivityAtUtc < inactiveSinceUtc)]);
}

internal static class TestOptions
{
    public static IOptions<SessionOptions> Session(int maxParticipants = 100, int toleranceMs = 500) =>
        Microsoft.Extensions.Options.Options.Create(new SessionOptions
        {
            MaxParticipants = maxParticipants,
            AnswerToleranceMs = toleranceMs
        });
}
