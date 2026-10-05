using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sage.Kahoot.IntegrationTests.Sessions;

/// <summary>
/// Cobre a presença no hub: quando <c>ParticipantLeft</c> é emitido, o total de ativos que ele
/// carrega, e o caso de múltiplas conexões do mesmo participante.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HubPresenceTests(ApiFixture fixture)
{
    /// <summary>
    /// Generoso porque o transporte é long polling: o servidor só percebe a queda ao fim do ciclo
    /// de poll, não no instante do dispose.
    /// </summary>
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Long polling em vez de WebSocket: o TestServer só expõe WebSocket com configuração extra,
    /// e o comportamento de presença é idêntico nos dois transportes.
    /// </summary>
    private HubConnection BuildConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Server.BaseAddress, "hubs/quiz"), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

    private async Task<(HttpClient Admin, JsonElement Session)> OpenSessionAsync(int questionCount = 1)
    {
        await fixture.WaitUntilReadyAsync();
        var admin = fixture.CreateClient();
        admin.UseBearer(await admin.LoginAsAdminAsync());

        var quiz = await admin.CreateQuizAsync($"Presença {Guid.NewGuid():N}", questionCount);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());

        return (admin, session);
    }

    [Fact]
    public async Task Queda_da_conexao_do_participante_emite_ParticipantLeft_para_o_apresentador()
    {
        var (admin, session) = await OpenSessionAsync();
        var sessionId = session.GetProperty("id").GetGuid();
        var pin = session.GetProperty("pin").GetString()!;

        using var anon = fixture.CreateClient();
        var ana = await anon.JoinAsync(pin, "Ana");
        var bruno = await anon.JoinAsync(pin, "Bruno");

        // Apresentador conectado e inscrito no grupo de host da sessão.
        var adminToken = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        await using var hostConnection = BuildConnection(adminToken);

        var saida = new TaskCompletionSource<ParticipantPresencePayload>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hostConnection.On<ParticipantPresencePayload>(
            nameof(Api.Hubs.IQuizClient.ParticipantLeft),
            payload => saida.TrySetResult(payload));

        await hostConnection.StartAsync();
        await hostConnection.InvokeAsync("JoinSessionGroup", sessionId);

        // Os dois participantes conectam; em seguida um deles cai.
        var anaConnection = BuildConnection(ana.GetProperty("accessToken").GetString()!);
        await using var brunoConnection = BuildConnection(bruno.GetProperty("accessToken").GetString()!);
        await anaConnection.StartAsync();
        await brunoConnection.StartAsync();

        await anaConnection.DisposeAsync();

        var recebido = await saida.Task.WaitAsync(EventTimeout);

        Assert.Equal("Ana", recebido.Nickname);
        Assert.Equal(sessionId, recebido.SessionId);
        // Bruno segue conectado: o total de ativos anunciado é 1.
        Assert.Equal(1, recebido.TotalParticipants);
    }

    [Fact]
    public async Task Queda_de_uma_entre_duas_conexoes_do_mesmo_participante_nao_emite_saida()
    {
        var (admin, session) = await OpenSessionAsync();
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        var ana = await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");
        var anaToken = ana.GetProperty("accessToken").GetString()!;

        var adminToken = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        await using var hostConnection = BuildConnection(adminToken);

        var houveSaida = false;
        hostConnection.On<ParticipantPresencePayload>(
            nameof(Api.Hubs.IQuizClient.ParticipantLeft),
            _ => houveSaida = true);

        await hostConnection.StartAsync();
        await hostConnection.InvokeAsync("JoinSessionGroup", sessionId);

        // Ana abre duas abas e fecha uma.
        var primeiraAba = BuildConnection(anaToken);
        await using var segundaAba = BuildConnection(anaToken);
        await primeiraAba.StartAsync();
        await segundaAba.StartAsync();

        await primeiraAba.DisposeAsync();

        // Sem evento esperado: dar tempo suficiente para que ele apareceria se fosse emitido.
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(houveSaida, "Fechar uma aba não pode anunciar saída enquanto outra segue aberta.");

        // E o participante continua contado como ativo.
        var state = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/state");
        Assert.Equal(1, state.GetProperty("participantCount").GetInt32());
    }
}
