using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Sage.Kahoot.Application.Auth.AdminLogin;

/// <summary>
/// Login do apresentador. O IP vem do transporte apenas para registro em log —
/// não participa da decisão de autenticação.
/// </summary>
public sealed record AdminLoginCommand(string Username, string Password, string? ClientIp)
    : ICommand<Result<AdminLoginResult>>;

public sealed record AdminLoginResult(string AccessToken, DateTime ExpiresAtUtc, string Role);

public sealed class AdminLoginHandler(
    IAdminCredentialValidator credentials,
    ITokenIssuer tokenIssuer,
    ILogger<AdminLoginHandler> logger)
    : ICommandHandler<AdminLoginCommand, Result<AdminLoginResult>>
{
    /// <summary>
    /// Mensagem de falha deliberadamente genérica: não distingue usuário inexistente de senha incorreta.
    /// </summary>
    private static readonly DomainError InvalidCredentials =
        DomainError.Unauthorized("auth.invalid_credentials", "Credenciais inválidas.");

    public Task<Result<AdminLoginResult>> ExecuteAsync(AdminLoginCommand command, CancellationToken ct)
    {
        if (!credentials.Validate(command.Username, command.Password))
        {
            // Registra resultado e origem; jamais a senha enviada.
            logger.LogWarning("Login de admin negado para o IP {ClientIp}", command.ClientIp ?? "desconhecido");
            return Task.FromResult(Result<AdminLoginResult>.Failure(InvalidCredentials));
        }

        var token = tokenIssuer.IssueAdminToken();

        logger.LogInformation("Login de admin aceito para o IP {ClientIp}", command.ClientIp ?? "desconhecido");

        return Task.FromResult(Result<AdminLoginResult>.Success(
            new AdminLoginResult(token.AccessToken, token.ExpiresAtUtc, AuthClaims.AdminRole)));
    }
}
