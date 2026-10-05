using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions.GetPartialRanking;
using Sage.Kahoot.Infrastructure.Auth;

namespace Sage.Kahoot.Api.Features.Sessions.GetRanking;

public sealed record GetRankingRequest(Guid SessionId);

/// <summary>
/// Ranking corrente sob demanda. Complementa o push de <c>RankingUpdated</c>: quem recarrega o
/// telão entre perguntas fica sem pódio até o próximo evento, e <c>GetSessionState</c> devolve o
/// estado da pergunta, não as posições.
/// </summary>
public sealed class GetRankingEndpoint : Endpoint<GetRankingRequest, RankingPayload>
{
    public override void Configure()
    {
        Get("/sessions/{sessionId}/ranking");
        Roles(AuthClaims.AdminRole, AuthClaims.ParticipantRole);

        Summary(s => s.Summary =
            "Ranking corrente da sessão, marcado como final quando a sessão já encerrou.");

        Description(b => b
            .Produces<RankingPayload>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetRankingRequest request, CancellationToken ct)
    {
        // Admin consulta qualquer sessão; participante só a do seu token.
        if (User.ParticipantId() is not null && User.SessionId() != request.SessionId)
        {
            await Send.ResultAsync(Domain.Sessions.SessionErrors.ParticipantNotInSession.ToProblem());
            return;
        }

        var result = await new GetPartialRankingQuery(request.SessionId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
