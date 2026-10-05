using System.Diagnostics;
using Sage.Kahoot.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Sage.Kahoot.Api.Common;

/// <summary>
/// Tradução única de <see cref="Result{T}"/> para resposta HTTP, usada por todos os endpoints.
/// Centralizar aqui é o que mantém o mapeamento categoria → status consistente e impede que
/// cada endpoint invente o seu.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();

    /// <summary>Para criação de recurso: 201 com Location apontando para o recurso criado.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : result.Error!.ToProblem();

    /// <summary>Para exclusão e comandos sem corpo de resposta.</summary>
    public static IResult ToNoContent<T>(this Result<T> result) =>
        result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();

    public static IResult ToProblem(this DomainError error)
    {
        var status = error.Category switch
        {
            ErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ErrorCategory.Conflict => StatusCodes.Status409Conflict,
            ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = new ProblemDetails
        {
            Type = $"https://sage-kahoot/errors/{error.Code}",
            Title = TitleFor(error.Category),
            Status = status,
            Detail = error.Message
        };

        problem.Extensions["errorCode"] = error.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id;

        return Results.Problem(problem);
    }

    private static string TitleFor(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => "Recurso não encontrado",
        ErrorCategory.Conflict => "Conflito com o estado atual",
        ErrorCategory.Unauthorized => "Não autorizado",
        _ => "Requisição inválida"
    };
}
