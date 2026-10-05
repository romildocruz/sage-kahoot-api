using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Sessions;
using MongoDB.Driver;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <inheritdoc cref="ISessionRepository"/>
public sealed class SessionRepository(MongoContext context) : ISessionRepository
{
    private static readonly int[] ActiveStatuses =
    [
        (int)SessionStatus.Aguardando,
        (int)SessionStatus.EmAndamento
    ];

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var document = await context.Sessions.Find(s => s.Id == id).FirstOrDefaultAsync(ct);
        return document is null ? null : DocumentMapper.ToDomain(document);
    }

    public async Task<Session?> GetActiveByPinAsync(string pin, CancellationToken ct)
    {
        var document = await context.Sessions
            .Find(s => s.Pin == pin && ActiveStatuses.Contains(s.Status))
            .FirstOrDefaultAsync(ct);

        return document is null ? null : DocumentMapper.ToDomain(document);
    }

    public Task InsertAsync(Session session, CancellationToken ct) =>
        context.Sessions.InsertOneAsync(DocumentMapper.ToDocument(session), cancellationToken: ct);

    public Task ReplaceAsync(Session session, CancellationToken ct) =>
        context.Sessions.ReplaceOneAsync(
            s => s.Id == session.Id,
            DocumentMapper.ToDocument(session),
            cancellationToken: ct);

    /// <summary>
    /// Substituição condicional ao índice da pergunta corrente (D5): se outro caminho de execução
    /// já avançou a sessão, esta escrita não se aplica e o chamador sabe que perdeu a corrida.
    /// </summary>
    public async Task<bool> ReplaceIfCurrentQuestionAsync(
        Session session,
        int expectedQuestionIndex,
        CancellationToken ct)
    {
        var result = await context.Sessions.ReplaceOneAsync(
            s => s.Id == session.Id && s.CurrentQuestionIndex == expectedQuestionIndex,
            DocumentMapper.ToDocument(session),
            cancellationToken: ct);

        return result.MatchedCount > 0;
    }

    public async Task<bool> TryCloseCurrentQuestionAsync(
        Guid sessionId,
        int questionIndex,
        DateTime closedAtUtc,
        CancellationToken ct)
    {
        var filter = Builders<SessionDocument>.Filter.And(
            Builders<SessionDocument>.Filter.Eq(s => s.Id, sessionId),
            Builders<SessionDocument>.Filter.Eq(s => s.CurrentQuestionIndex, questionIndex),
            Builders<SessionDocument>.Filter.Eq(s => s.CurrentQuestionClosed, false));

        var update = Builders<SessionDocument>.Update
            .Set(s => s.CurrentQuestionClosed, true)
            .Set(s => s.CurrentQuestionClosesAtUtc, closedAtUtc)
            .Set(s => s.LastActivityAtUtc, closedAtUtc);

        var result = await context.Sessions.UpdateOneAsync(filter, update, cancellationToken: ct);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> HasActiveSessionForQuizAsync(Guid quizId, CancellationToken ct) =>
        await context.Sessions
            .Find(s => s.QuizId == quizId && ActiveStatuses.Contains(s.Status))
            .AnyAsync(ct);

    public async Task<bool> PinInUseAsync(string pin, CancellationToken ct) =>
        await context.Sessions
            .Find(s => s.Pin == pin && ActiveStatuses.Contains(s.Status))
            .AnyAsync(ct);

    /// <summary>
    /// Insere a resposta contando com o índice único para rejeitar a duplicidade — sem leitura prévia,
    /// o que elimina a janela de corrida entre verificar e inserir.
    /// </summary>
    public async Task<bool> TryInsertAnswerAsync(AnswerRecord answer, CancellationToken ct)
    {
        try
        {
            await context.Answers.InsertOneAsync(DocumentMapper.ToDocument(answer), cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task IncrementParticipantScoreAsync(
        Guid sessionId,
        Guid participantId,
        int points,
        long responseTimeMs,
        CancellationToken ct)
    {
        var filter = Builders<SessionDocument>.Filter.And(
            Builders<SessionDocument>.Filter.Eq(s => s.Id, sessionId),
            Builders<SessionDocument>.Filter.ElemMatch(s => s.Participants, p => p.Id == participantId));

        var update = Builders<SessionDocument>.Update
            .Inc("participants.$.totalScore", points)
            .Inc("participants.$.totalResponseTimeMs", responseTimeMs);

        return context.Sessions.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public Task SetParticipantConnectedAsync(
        Guid sessionId,
        Guid participantId,
        bool isConnected,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var filter = Builders<SessionDocument>.Filter.And(
            Builders<SessionDocument>.Filter.Eq(s => s.Id, sessionId),
            Builders<SessionDocument>.Filter.ElemMatch(s => s.Participants, p => p.Id == participantId));

        var update = Builders<SessionDocument>.Update
            .Set("participants.$.isConnected", isConnected)
            .Set(s => s.LastActivityAtUtc, nowUtc);

        return context.Sessions.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<int> CountAnswersForQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct) =>
        (int)await context.Answers.CountDocumentsAsync(
            a => a.SessionId == sessionId && a.QuestionId == questionId,
            cancellationToken: ct);

    public async Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(Guid sessionId, CancellationToken ct)
    {
        var documents = await context.Answers
            .Find(a => a.SessionId == sessionId)
            .SortBy(a => a.QuestionIndex)
            .ToListAsync(ct);

        return [.. documents.Select(DocumentMapper.ToDomain)];
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetAnswersForQuestionAsync(
        Guid sessionId,
        Guid questionId,
        CancellationToken ct)
    {
        var documents = await context.Answers
            .Find(a => a.SessionId == sessionId && a.QuestionId == questionId)
            .ToListAsync(ct);

        return [.. documents.Select(DocumentMapper.ToDomain)];
    }

    public async Task<IReadOnlyList<OptionAnswerCount>> CountAnswersByOptionAsync(
        Guid sessionId,
        Guid questionId,
        CancellationToken ct)
    {
        var grouped = await context.Answers
            .Aggregate()
            .Match(a => a.SessionId == sessionId && a.QuestionId == questionId)
            .Group(a => a.SelectedOptionId, g => new { OptionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return [.. grouped.Select(static g => new OptionAnswerCount(g.OptionId, g.Count))];
    }

    public async Task<PagedResult<SessionListItem>> ListAsync(ListSessionsFilter filter, CancellationToken ct)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var builder = Builders<SessionDocument>.Filter;
        var predicate = builder.Empty;

        if (filter.FromUtc is not null)
        {
            predicate &= builder.Gte(s => s.CreatedAtUtc, filter.FromUtc.Value);
        }

        if (filter.ToUtc is not null)
        {
            predicate &= builder.Lte(s => s.CreatedAtUtc, filter.ToUtc.Value);
        }

        if (filter.Status is not null)
        {
            predicate &= builder.Eq(s => s.Status, (int)filter.Status.Value);
        }

        var total = await context.Sessions.CountDocumentsAsync(predicate, cancellationToken: ct);

        var documents = await context.Sessions
            .Find(predicate)
            .SortByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var items = documents
            .Select(static d => new SessionListItem(
                d.Id,
                d.Pin,
                d.QuizId,
                d.QuizSnapshot.Title,
                (SessionStatus)d.Status,
                d.CreatedAtUtc,
                d.StartedAtUtc,
                d.EndedAtUtc,
                d.Participants.Count,
                d.Participants.Count == 0 ? 0 : d.Participants.Max(static p => p.TotalScore)))
            .ToList();

        return new PagedResult<SessionListItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<Session>> GetWithExpiredQuestionAsync(DateTime nowUtc, CancellationToken ct)
    {
        var documents = await context.Sessions
            .Find(s => s.Status == (int)SessionStatus.EmAndamento
                && !s.CurrentQuestionClosed
                && s.CurrentQuestionClosesAtUtc != null
                && s.CurrentQuestionClosesAtUtc < nowUtc)
            .ToListAsync(ct);

        return [.. documents.Select(DocumentMapper.ToDomain)];
    }

    public async Task<IReadOnlyList<Session>> GetAbandonedAsync(DateTime inactiveSinceUtc, CancellationToken ct)
    {
        var documents = await context.Sessions
            .Find(s => ActiveStatuses.Contains(s.Status) && s.LastActivityAtUtc < inactiveSinceUtc)
            .ToListAsync(ct);

        return [.. documents.Select(DocumentMapper.ToDomain)];
    }
}
