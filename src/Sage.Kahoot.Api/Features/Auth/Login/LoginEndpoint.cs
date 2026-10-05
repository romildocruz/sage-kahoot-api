using FastEndpoints;
using FluentValidation;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Auth.AdminLogin;
using Sage.Kahoot.Infrastructure.Configuration;

namespace Sage.Kahoot.Api.Features.Auth.Login;

public sealed record LoginRequest(string Username, string Password);

/// <summary>Forma do request apenas. A verificação da credencial é do handler.</summary>
public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class LoginEndpoint : Endpoint<LoginRequest, AdminLoginResult>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitingOptions.LoginPolicy));

        Summary(s =>
        {
            s.Summary = "Autentica o apresentador e emite o token de admin.";
            s.Description = "Credencial única configurada via Secret. Limitado por IP para conter força bruta.";
        });

        Description(b => b
            .Produces<AdminLoginResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
    }

    public override async Task HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var result = await new AdminLoginCommand(
            request.Username,
            request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString()).ExecuteAsync(ct);

        await Send.ResultAsync(result.ToHttp());
    }
}
