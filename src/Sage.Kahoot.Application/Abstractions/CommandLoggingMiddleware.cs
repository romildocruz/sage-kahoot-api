using System.Diagnostics;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Sage.Kahoot.Application.Abstractions;

/// <summary>
/// Correlação e log estruturado de todo despacho do command bus: nome do caso de uso, duração e desfecho.
/// Registra apenas o nome do comando — nunca seu conteúdo — para que credenciais enviadas no
/// login nunca cheguem ao log (ver spec api-platform, "Logs estruturados e correlacionáveis").
/// </summary>
public sealed class CommandLoggingMiddleware<TCommand, TResult>(ILogger<TCommand> logger)
    : ICommandMiddleware<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        var commandName = typeof(TCommand).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();
            stopwatch.Stop();

            logger.LogInformation(
                "Comando {CommandName} concluído em {ElapsedMs} ms com {Outcome}",
                commandName,
                stopwatch.ElapsedMilliseconds,
                DescribeOutcome(result));

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(
                ex,
                "Comando {CommandName} lançou exceção após {ElapsedMs} ms",
                commandName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Extrai o desfecho sem depender do tipo fechado de Result: uma falha previsível de regra
    /// aparece no log como o código do erro, não como exceção.
    /// </summary>
    private static string DescribeOutcome(TResult result)
    {
        if (result is null)
        {
            return "sucesso";
        }

        var type = result.GetType();

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Domain.Common.Result<>))
        {
            return "sucesso";
        }

        var error = type.GetProperty(nameof(Domain.Common.Result<int>.Error))?.GetValue(result);

        return error is Domain.Common.DomainError domainError
            ? $"falha {domainError.Code}"
            : "sucesso";
    }
}
