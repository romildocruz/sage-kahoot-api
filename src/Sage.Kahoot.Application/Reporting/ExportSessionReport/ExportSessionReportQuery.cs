using System.Globalization;
using System.Text;
using System.Text.Json;
using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Reporting.GetSessionReport;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Reporting.ExportSessionReport;

public enum ReportFormat
{
    Csv,
    Json
}

public sealed record ExportSessionReportQuery(Guid SessionId, ReportFormat Format)
    : ICommand<Result<ExportedReport>>;

public sealed record ExportedReport(string FileName, string ContentType, byte[] Content);

public sealed class ExportSessionReportHandler(ISessionRepository sessions)
    : ICommandHandler<ExportSessionReportQuery, Result<ExportedReport>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<Result<ExportedReport>> ExecuteAsync(ExportSessionReportQuery query, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(query.SessionId, ct);

        if (session is null)
        {
            return Result<ExportedReport>.Failure(SessionErrors.NotFound);
        }

        var answers = await sessions.GetAnswersAsync(session.Id, ct);
        var report = GetSessionReportHandler.Build(session, answers);
        var stamp = (report.StartedAtUtc ?? report.CreatedAtUtc).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var slug = Slugify(report.QuizTitle);

        return query.Format switch
        {
            ReportFormat.Csv => new ExportedReport(
                $"sessao-{slug}-{stamp}.csv",
                "text/csv; charset=utf-8",
                // BOM para que o Excel abra o arquivo em UTF-8 sem corromper acentuação.
                [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(BuildCsv(report))]),

            _ => new ExportedReport(
                $"sessao-{slug}-{stamp}.json",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, JsonOptions)))
        };
    }

    /// <summary>Uma linha por participante, com cabeçalho na primeira linha.</summary>
    private static string BuildCsv(SessionReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "posicao,apelido,pontuacao,acertos,erros,nao_respondidas,tempo_medio_ms");

        foreach (var entry in report.Ranking)
        {
            builder.Append(entry.Position).Append(',')
                .Append(EscapeCsv(entry.Nickname)).Append(',')
                .Append(entry.TotalScore).Append(',')
                .Append(entry.CorrectCount).Append(',')
                .Append(entry.WrongCount).Append(',')
                .Append(entry.UnansweredCount).Append(',')
                .Append(entry.AverageResponseTimeMs.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Apelido é texto livre do participante: vírgula, aspas ou quebra de linha precisam ser
    /// escapadas ou destroem o alinhamento das colunas.
    /// </summary>
    private static string EscapeCsv(string value)
    {
        var needsQuoting = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        return needsQuoting
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string Slugify(string title)
    {
        var builder = new StringBuilder(title.Length);

        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length == 0 ? "quiz" : slug[..Math.Min(slug.Length, 40)];
    }
}
