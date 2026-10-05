using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Sage.Kahoot.Api.Middleware;

/// <summary>
/// Rede de segurança para o que escapou dos handlers. Falhas previsíveis de regra viram
/// <c>DomainError</c> e nunca chegam aqui; o que chega é defeito, e o cliente recebe uma
/// resposta genérica enquanto o log guarda o diagnóstico completo.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu da requisição: não é erro do servidor e não deve poluir o log.
            logger.LogDebug("Requisição {Path} cancelada pelo cliente", context.Request.Path);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            logger.LogError(
                ex,
                "Falha não tratada em {Method} {Path} (traceId {TraceId})",
                context.Request.Method,
                context.Request.Path,
                traceId);

            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Sem stack trace, sem nome de tipo interno, sem fragmento de query no corpo.
            var problem = new ProblemDetails
            {
                Type = "https://sage-kahoot/errors/internal",
                Title = "Erro interno",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Ocorreu um erro inesperado. Informe o traceId ao suporte."
            };

            problem.Extensions["traceId"] = traceId;

            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}

/// <summary>
/// Garante um identificador de correlação por requisição e o devolve no cabeçalho,
/// para ligar o que o usuário viu ao que ficou registrado no log.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var provided)
            && !string.IsNullOrWhiteSpace(provided)
                ? provided.ToString()
                : Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        return next(context);
    }
}
