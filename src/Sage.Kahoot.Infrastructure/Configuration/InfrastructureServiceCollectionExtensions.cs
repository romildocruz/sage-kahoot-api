using System.Text;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Infrastructure.Auth;
using Sage.Kahoot.Infrastructure.Mongo;
using Sage.Kahoot.Infrastructure.Sessions;
using Sage.Kahoot.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Infrastructure.Configuration;

/// <summary>
/// Composição da camada de infraestrutura: opções validadas na inicialização, acesso ao Mongo,
/// relógio, gerador de PIN e emissor de token.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<MongoOptions>(configuration, MongoOptions.SectionName);
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            // Com AllowCredentials o curinga é inválido e derrubaria o CORS inteiro em silêncio.
            .Validate(
                static cors => !Array.Exists(cors.AllowedOrigins, static o => o == "*"),
                "Cors__AllowedOrigins não aceita curinga: liste as origens internas explicitamente.")
            .ValidateOnStart();
        services.AddValidatedOptions<Application.Abstractions.SessionOptions>(
            configuration,
            Application.Abstractions.SessionOptions.SectionName);
        services.AddValidatedOptions<RateLimitingOptions>(configuration, RateLimitingOptions.SectionName);

        services.AddOptions<SwaggerOptions>()
            .Bind(configuration.GetSection(SwaggerOptions.SectionName));

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        // A chave de assinatura tem uma exigência que DataAnnotations não expressa bem:
        // comprimento mínimo em bytes para HS256. Falha rápida no startup, sem registrar o valor.
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static auth => Encoding.UTF8.GetByteCount(auth.SigningKey ?? string.Empty) >= AuthOptions.MinimumSigningKeyBytes,
                $"Auth__SigningKey deve ter no mínimo {AuthOptions.MinimumSigningKeyBytes} bytes para HS256.")
            .ValidateOnStart();

        services.AddSingleton<MongoContext>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPinGenerator, SecurePinGenerator>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IAdminCredentialValidator, AdminCredentialValidator>();

        // Singleton, e não Scoped: os repositórios são stateless sobre o MongoContext singleton,
        // cujo IMongoClient é thread-safe. Registrá-los como Scoped quebrava todo despacho de
        // comando feito fora de uma requisição HTTP — o hub SignalR e o worker de expiração —,
        // porque o command bus do FastEndpoints resolve handlers a partir do provider raiz.
        services.AddSingleton<IQuizRepository, QuizRepository>();
        services.AddSingleton<ISessionRepository, SessionRepository>();

        return services;
    }

    private static void AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
