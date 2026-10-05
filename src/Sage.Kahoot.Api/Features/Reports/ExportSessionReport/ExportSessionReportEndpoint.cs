using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Reporting.ExportSessionReport;

namespace Sage.Kahoot.Api.Features.Reports.ExportSessionReport;

public sealed record ExportSessionReportRequest(Guid SessionId, string Format = "csv");

public sealed class ExportSessionReportEndpoint : Endpoint<ExportSessionReportRequest>
{
    public override void Configure()
    {
        Get("/reports/sessions/{sessionId}/export");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary = "Exporta o relatório da sessão em CSV ou JSON (?format=csv|json).");

        Description(b => b
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(ExportSessionReportRequest request, CancellationToken ct)
    {
        if (!TryParseFormat(request.Format, out var format))
        {
            await Send.ResultAsync(Domain.Common.DomainError
                .Validation("report.invalid_format", "Formato inválido. Use csv ou json.")
                .ToProblem());
            return;
        }

        var result = await new ExportSessionReportQuery(request.SessionId, format).ExecuteAsync(ct);

        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.Error!.ToProblem());
            return;
        }

        await Send.BytesAsync(
            result.Value.Content,
            result.Value.FileName,
            result.Value.ContentType,
            cancellation: ct);
    }

    private static bool TryParseFormat(string? value, out ReportFormat format)
    {
        format = ReportFormat.Csv;

        return value?.ToLowerInvariant() switch
        {
            null or "" or "csv" => true,
            "json" => (format = ReportFormat.Json) == ReportFormat.Json,
            _ => false
        };
    }
}
