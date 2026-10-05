using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace Sage.Kahoot.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe um MongoDB real em contêiner e aponta a API para ele.
/// </summary>
/// <remarks>
/// Exige Docker na máquina de build. Os testes de integração ficam neste projeto separado
/// justamente para que a suíte unitária continue rodando onde não há Docker.
/// </remarks>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "apresentador-teste";
    public const string AdminPassword = "senha-sintetica-de-teste";
    public const string SigningKey = "chave-sintetica-de-teste-com-mais-de-32-bytes";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7.0").Build();

    public string ConnectionString => _mongo.GetConnectionString();

    public async Task InitializeAsync() => await _mongo.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
                ["Mongo:Database"] = $"sage_kahoot_test_{Guid.NewGuid():N}",
                ["Auth:AdminUsername"] = AdminUsername,
                ["Auth:AdminPassword"] = AdminPassword,
                ["Auth:SigningKey"] = SigningKey,
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["Seed:Enabled"] = "false",
                // Limites altos para que o rate limiting não interfira nos testes que não o exercitam.
                ["RateLimiting:LoginPermitLimit"] = "1000",
                ["RateLimiting:JoinPermitLimit"] = "1000"
            });
        });
    }

    /// <summary>Aguarda o inicializador em segundo plano criar os índices antes de exercitar a API.</summary>
    public async Task WaitUntilReadyAsync(TimeSpan? timeout = null)
    {
        using var client = CreateClient();
        var deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/health/ready");

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("A API não ficou pronta dentro do tempo esperado.");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
