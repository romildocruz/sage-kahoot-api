using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Quizzes.ListQuizzes;

public sealed record ListQuizzesQuery(int Page, int PageSize, string? TitleContains)
    : ICommand<Result<PagedResult<QuizListItem>>>;

public sealed class ListQuizzesHandler(IQuizRepository quizzes)
    : ICommandHandler<ListQuizzesQuery, Result<PagedResult<QuizListItem>>>
{
    public async Task<Result<PagedResult<QuizListItem>>> ExecuteAsync(ListQuizzesQuery query, CancellationToken ct)
    {
        var page = await quizzes.ListAsync(
            new ListQuizzesFilter(query.Page, query.PageSize, query.TitleContains),
            ct);

        return page;
    }
}
