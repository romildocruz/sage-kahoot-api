using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes.ListQuizzes;

namespace Sage.Kahoot.Api.Features.Quizzes.ListQuizzes;

/// <summary>Parâmetros de query. Página e tamanho são normalizados na camada de aplicação.</summary>
public sealed record ListQuizzesRequest(int Page = 1, int PageSize = Paging.DefaultPageSize, string? Title = null);

public sealed class ListQuizzesEndpoint : Endpoint<ListQuizzesRequest, PagedResult<QuizListItem>>
{
    public override void Configure()
    {
        Get("/quizzes");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            $"Lista quizzes ordenados por atualização decrescente. Tamanho de página máximo: {Paging.MaxPageSize}.");

        Description(b => b.Produces<PagedResult<QuizListItem>>(StatusCodes.Status200OK));
    }

    public override async Task HandleAsync(ListQuizzesRequest request, CancellationToken ct)
    {
        var result = await new ListQuizzesQuery(request.Page, request.PageSize, request.Title).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
