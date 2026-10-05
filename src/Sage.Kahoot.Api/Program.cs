using FastEndpoints;
using FastEndpoints.Swagger;
using Sage.Kahoot.Api.Configuration;
using Sage.Kahoot.Api.Hubs;
using Sage.Kahoot.Api.Middleware;
using Sage.Kahoot.Api.Realtime;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.ParticipantPresence;
using Sage.Kahoot.Infrastructure.Configuration;
using Sage.Kahoot.Infrastructure.Mongo;
using Sage.Kahoot.Infrastructure.Seed;
using Sage.Kahoot.Infrastructure.Sessions;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseStructuredLogging();

// Infraestrutura: opções validadas na inicialização (falha rápida), Mongo, relógio, PIN e token.
builder.Services.AddInfrastructure(builder.Configuration);

// FastEndpoints varre os dois assemblies: os endpoints vivem na Api, os handlers na Application.
builder.Services.AddFastEndpoints(options =>
{
    options.Assemblies =
    [
        typeof(Program).Assembly,
        typeof(SessionBroadcaster).Assembly
    ];
});

builder.Services.AddCommandMiddleware(c => c.Register(typeof(CommandLoggingMiddleware<,>)));

builder.Services.AddApiAuthentication();
builder.Services.AddApiCors();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiHealthChecks();

builder.Services.AddSignalR(options =>
{
    // Limites por conexão: uma conexão abusiva não deve derrubar a sessão dos demais.
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Singleton pelo mesmo motivo dos repositórios: é stateless e precisa ser resolvível
// a partir do provider raiz, que é de onde o command bus resolve os handlers.
builder.Services.AddSingleton<SessionBroadcaster>();
builder.Services.AddSingleton<ParticipantConnectionTracker>();
builder.Services.AddSingleton<ISessionNotifier, SignalRSessionNotifier>();
builder.Services.AddSingleton<SyntheticQuizSeeder>();
builder.Services.AddSingleton<MongoReadinessState>();
builder.Services.AddHostedService<MongoInitializer>();
builder.Services.AddHostedService<QuestionExpiryWorker>();
builder.Services.AddHostedService<AbandonedSessionSweeper>();

var swaggerEnabled = builder.IsSwaggerEnabled();

if (swaggerEnabled)
{
    builder.Services.AddApiSwagger();
}

var app = builder.Build();

// Primeiro da cadeia: nada que falhe adiante escapa sem virar problem+json.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLogging();

app.UseCors(CorsOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api/v1";
    // ProblemDetails RFC 7807/9457 nativo, cobrindo as falhas de Validator<TRequest>.
    config.Errors.UseProblemDetails();
});

if (swaggerEnabled)
{
    app.UseSwaggerGen();
}

app.MapHub<QuizHub>(AuthSetup.HubPath);
app.MapApiHealthChecks();

try
{
    await app.RunAsync();
}
catch (OptionsValidationException ex)
{
    // Configuração obrigatória ausente ou inválida: a mensagem nomeia a chave, nunca o valor.
    foreach (var failure in ex.Failures)
    {
        Log.Fatal("Configuração inválida: {Failure}", failure);
    }

    Environment.ExitCode = 78; // EX_CONFIG
}
catch (Exception ex)
{
    Log.Fatal(ex, "sage-kahoot-api encerrou de forma inesperada");
    Environment.ExitCode = 70; // EX_SOFTWARE
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposto para o WebApplicationFactory dos testes de integração.</summary>
public partial class Program;
