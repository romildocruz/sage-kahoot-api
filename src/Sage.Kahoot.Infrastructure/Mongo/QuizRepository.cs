using System.Text.RegularExpressions;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Quizzes;
using MongoDB.Driver;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <inheritdoc cref="IQuizRepository"/>
public sealed class QuizRepository(MongoContext context) : IQuizRepository
{
    public async Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var document = await context.Quizzes
            .Find(q => q.Id == id)
            .FirstOrDefaultAsync(ct);

        return document is null ? null : DocumentMapper.ToDomain(document);
    }

    public async Task<PagedResult<QuizListItem>> ListAsync(ListQuizzesFilter filter, CancellationToken ct)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var predicate = string.IsNullOrWhiteSpace(filter.TitleContains)
            ? Builders<QuizDocument>.Filter.Empty
            : Builders<QuizDocument>.Filter.Regex(
                q => q.Title,
                new MongoDB.Bson.BsonRegularExpression(Regex.Escape(filter.TitleContains.Trim()), "i"));

        var total = await context.Quizzes.CountDocumentsAsync(predicate, cancellationToken: ct);

        var documents = await context.Quizzes
            .Find(predicate)
            .SortByDescending(q => q.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var items = documents
            .Select(static d => new QuizListItem(
                d.Id,
                d.Title,
                d.Description,
                d.Questions.Count,
                d.CreatedAtUtc,
                d.UpdatedAtUtc))
            .ToList();

        return new PagedResult<QuizListItem>(items, total, page, pageSize);
    }

    public Task InsertAsync(Quiz quiz, CancellationToken ct) =>
        context.Quizzes.InsertOneAsync(DocumentMapper.ToDocument(quiz), cancellationToken: ct);

    public Task ReplaceAsync(Quiz quiz, CancellationToken ct) =>
        context.Quizzes.ReplaceOneAsync(q => q.Id == quiz.Id, DocumentMapper.ToDocument(quiz), cancellationToken: ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await context.Quizzes.DeleteOneAsync(q => q.Id == id, ct);
        return result.DeletedCount > 0;
    }

    public async Task<bool> SeedQuizExistsAsync(CancellationToken ct) =>
        await context.Quizzes.Find(q => q.IsSeed).AnyAsync(ct);
}
