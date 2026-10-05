using System.Text;
using Sage.Kahoot.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AppCorsOptions = Sage.Kahoot.Infrastructure.Configuration.CorsOptions;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace Sage.Kahoot.Api.Configuration;

/// <summary>Autenticação JWT e as duas políticas de autorização de D6.</summary>
public static class AuthSetup
{
    /// <summary>Caminho do hub; o token na query string só é aceito aqui.</summary>
    public const string HubPath = "/hubs/quiz";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // As opções do JwtBearer resolvem AuthOptions pelo DI, e não de um snapshot da
        // IConfiguration capturado no registro: ler a configuração cedo demais pega valores
        // anteriores a qualquer fonte adicionada depois (o caso do host de teste) e ainda
        // contorna a validação de ValidateOnStart.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<Infrastructure.Configuration.AuthOptions>>((jwt, authOptions) =>
            {
                var auth = authOptions.Value;

                // Sem isso, o handler renomeia "role" para a URI longa de ClaimTypes.Role e as
                // políticas — que casam pelo nome curto — passam a falhar com 403.
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = auth.Issuer,
                    ValidAudience = auth.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
                    RoleClaimType = AuthClaims.Role,
                    // Sem folga no relógio: token expirado é token expirado.
                    ClockSkew = TimeSpan.Zero
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // WebSocket não permite cabeçalho customizado no handshake, então o SignalR
                        // envia o token por query string. Aceito apenas no caminho do hub.
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments(HubPath))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthClaims.AdminPolicy, policy =>
                policy.RequireClaim(AuthClaims.Role, AuthClaims.AdminRole))
            .AddPolicy(AuthClaims.ParticipantPolicy, policy =>
                policy.RequireClaim(AuthClaims.Role, AuthClaims.ParticipantRole));

        return services;
    }

    /// <summary>
    /// CORS restrito às origens configuradas. A política também resolve as opções pelo DI;
    /// a recusa do curinga acontece na validação de inicialização (ver AddInfrastructure).
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        services.AddCors();

        services.AddOptions<AspNetCorsOptions>()
            .Configure<IOptions<AppCorsOptions>>((cors, appCors) =>
                cors.AddPolicy(AppCorsOptions.PolicyName, policy =>
                    policy.WithOrigins(appCors.Value.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .WithExposedHeaders(Middleware.CorrelationIdMiddleware.HeaderName)));

        return services;
    }
}
