using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Quizzes.GetQuizById;

public sealed record GetQuizByIdQuery(Guid QuizId) : ICommand<Result<QuizDto>>;

public sealed class GetQuizByIdHandler(IQuizRepository quizzes)
    : ICommandHandler<GetQuizByIdQuery, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> ExecuteAsync(GetQuizByIdQuery query, CancellationToken ct)
    {
        var quiz = await quizzes.GetByIdAsync(query.QuizId, ct);

        return quiz is null
            ? Result<QuizDto>.Failure(QuizErrors.NotFound)
            : QuizMapper.ToDto(quiz);
    }
}
