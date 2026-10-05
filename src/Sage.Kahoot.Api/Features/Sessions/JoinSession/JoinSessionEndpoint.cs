using FastEndpoints;
using FluentValidation;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.JoinSession;
using Sage.Kahoot.Domain.Sessions;
using Sage.Kahoot.Infrastructure.Configuration;

namespace Sage.Kahoot.Api.Features.Sessions.JoinSession;

public sealed record JoinSessionRequest(string Pin, string Nickname);

public sealed class JoinSessionValidator : Validator<JoinSessionRequest>
{
    public JoinSessionValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty()
            .Length(SessionRules.PinLength)
            .Matches("^[0-9]+$").WithMessage("O PIN deve conter apenas dígitos.");

        // A sanitização do apelido acontece no handler; aqui só limitamos o tamanho bruto recebido.
        RuleFor(x => x.Nickname)
            .NotEmpty()
            .MaximumLength(SessionRules.NicknameMaxLength * 2);
    }
}

public sealed class JoinSessionEndpoint : Endpoint<JoinSessionRequest, JoinSessionResult>
{
    public override void Configure()
    {
        Post("/sessions/join");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitingOptions.JoinPolicy));

        Summary(s =>
        {
            s.Summary = "Entrada anônima do participante por PIN e apelido.";
            s.Description =
                "Sem login. Devolve um token de participante vinculado à sessão, usado para responder e conectar ao hub. " +
                "Limitado por IP para conter varredura de PINs.";
        });

        Description(b => b
            .Produces<JoinSessionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
    }

    public override async Task HandleAsync(JoinSessionRequest request, CancellationToken ct)
    {
        var result = await new JoinSessionCommand(request.Pin, request.Nickname).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
