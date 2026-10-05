using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Reporting;
using Sage.Kahoot.Application.Reporting.GetSessionReport;

namespace Sage.Kahoot.Api.Features.Reports.GetSessionReport;

public sealed record GetSessionReportRequest(Guid SessionId);

public sealed class GetSessionReportEndpoint : Endpoint<GetSessionReportRequest, SessionReport>
{
    public override void Configure()
    {
        Get("/reports/sessions/{sessionId}");
        Policies(AuthClaims.AdminPolicy);

        Summary(s =>
        {
            s.Summary = "Relatório da sessão: ranking com pódio e estatísticas por pergunta.";
            s.Description =
                "Tudo recalculado a partir das respostas persistidas. Sessão ainda em andamento devolve ranking parcial, marcado como não final.";
        });

        Description(b => b
            .Produces<SessionReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetSessionReportRequest request, CancellationToken ct)
    {
        var result = await new GetSessionReportQuery(request.SessionId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
