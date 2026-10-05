# Cobertura das specs — bootstrap-sage-kahoot-api

Cada requisito de `openspec/changes/archive/2026-09-17-bootstrap-sage-kahoot-api/specs/` mapeado para onde foi
implementado e onde é verificado.

Legenda de verificação:
**U** = teste unitário (roda sem Docker) · **I** = teste de integração (exige Docker) ·
**M** = verificado manualmente com a API no ar · **—** = sem teste automatizado

**Status em 2026-09-18:** 76 testes unitários + 33 de integração, todos passando contra um
MongoDB 7.0 real em contêiner. Tudo marcado como **I** foi efetivamente executado.

## api-platform

| Requisito | Implementação | Verificação |
|---|---|---|
| Configuração externalizada sem segredos | `Infrastructure/Configuration/Options.cs`, `InfrastructureServiceCollectionExtensions` (`ValidateOnStart`) | **M** (falha nomeando `Mongo__ConnectionString`) + varredura de segredos versionados |
| Health checks liveness/readiness | `Api/Configuration/HealthChecksSetup.cs`, `Mongo/MongoInitializer.cs` | **M** (`live` 200 / `ready` 503 com Mongo fora), **I** `PlatformTests` |
| Documentação OpenAPI navegável | `Api/Configuration/SwaggerSetup.cs` (NSwag) | **M** (`/swagger` 200 em Development) |
| Respostas de erro padronizadas | `Errors.UseProblemDetails()`, `Api/Common/ResultExtensions.cs`, `ExceptionHandlingMiddleware` | **M** (400 listando campos), **I** `PlatformTests` |
| Logs estruturados e correlacionáveis | `Api/Configuration/LoggingSetup.cs`, `CorrelationIdMiddleware`, `CommandLoggingMiddleware` | **M** (JSON compacto no stdout) |
| CORS restrito à rede interna | `Api/Configuration/AuthSetup.cs` (curinga recusado na inicialização) | **—** |
| Rate limiting em endpoints sensíveis | `Api/Configuration/RateLimitingSetup.cs` | **M** (429 + `Retry-After: 60`) |
| Ausência de dados pessoais reais | `JoinSessionHandler`, `ParticipantDocument` | **U** `SessionHandlerTests` |

## admin-auth

| Requisito | Implementação | Verificação |
|---|---|---|
| Login com credencial única | `Application/Auth/AdminLogin`, `Infrastructure/Auth/AdminCredentialValidator` | **M** (200 com token / 401 genérico), **I** `PlatformTests` |
| Token assinado com expiração curta | `Infrastructure/Auth/JwtTokenIssuer` | **M**, **I** (assinatura inválida → 401) |
| Proteção das rotas administrativas | `AuthSetup` (políticas `AdminOnly`/`SessionParticipant`) | **M** (401 sem token), **I** `PlatformTests` |
| Autenticação do hub em tempo real | `AuthSetup.OnMessageReceived`, `QuizHub.EnsureAdmin` | **—** (requer cliente SignalR) |
| Credencial configurada de forma segura | `AuthOptions` + validação de 32 bytes no startup | **M** |

## quiz-management

| Requisito | Implementação | Verificação |
|---|---|---|
| Criação de quiz | `Application/Quizzes/CreateQuiz` | **U** `QuizHandlerTests`, **I** `QuizCrudTests` |
| Estrutura e validação da pergunta | `Domain/Quizzes/Question`, `Api/Features/Quizzes/QuizRequests` | **U** `QuizTests` (6 cenários), **M** (400 com campos) |
| Listagem e consulta | `GetQuizById`, `ListQuizzes` | **I** `QuizCrudTests` |
| Edição de quiz e perguntas | `UpdateQuiz` | **U** (all-or-nothing), **I** |
| Edição de quiz em uso por sessão ativa | `UpdateQuizHandler`, `DeleteQuizHandler` | **U** (409), **I** `QuizCrudTests` |
| Duplicação de quiz | `DuplicateQuiz`, `Quiz.Duplicate` | **U** (independência), **I** |
| Exclusão de quiz | `DeleteQuiz` + snapshot da sessão | **U**, **I** (relatório sobrevive à exclusão) |
| Dados de exemplo sintéticos | `Infrastructure/Seed/SyntheticQuizSeeder` | **—** (idempotência via `SeedQuizExistsAsync`) |

## live-session

| Requisito | Implementação | Verificação |
|---|---|---|
| Abertura de sessão a partir de um quiz | `OpenSession` + `QuizSnapshot` | **U** `SessionTests`, **I** `SessionFlowTests` |
| Geração e unicidade do PIN | `SecurePinGenerator`, índice único parcial | **I** (PIN de 6 dígitos), **U** (PIN encerrado não resolve) |
| Entrada anônima do participante | `JoinSession` + sanitização | **U** `SessionHandlerTests` (8 cenários), **I** |
| Limite de participantes por sessão | `Session.Join` | **U** |
| Estados e transições da sessão | `Domain/Sessions/Session` | **U** `SessionTests`, **I** (409 em transição inválida) |
| Avanço de perguntas pelo apresentador | `AdvanceQuestion`, `CloseQuestion` | **U**, **I** |
| Registro de resposta do participante | `SubmitAnswer` + índice único | **U** (janela/tolerância), **I** `ConcurrencyTests` |
| Pontuação por acerto e velocidade | `Domain/Sessions/Scoring` | **U** `ScoringTests` (6 cenários) |
| Ranking parcial entre perguntas | `Session.Ranking`, `SessionBroadcaster` | **U** (dois critérios de desempate) |
| Reconexão do participante | `GetSessionState`, token de participante | **I** (isolamento entre sessões) |
| Sessões abandonadas | `AbandonedSessionSweeper` | **—** |
| Presença do participante na sessão | `Session.MarkParticipant*`, `ParticipantConnectionTracker`, `QuizHub` | **U** `ParticipantPresenceTests` (5 cenários) + `ParticipantConnectionTrackerTests` (6), **I** `HubPresenceTests` |
| Consulta de ranking sob demanda | `Features/Sessions/GetRanking`, `QuizHub.GetRanking` | **I** `RankingQueryTests` (6 cenários) |

## realtime-hub

| Requisito | Implementação | Verificação |
|---|---|---|
| Endpoint único de hub | `Api/Hubs/QuizHub` em `/hubs/quiz` | **I** `HubPresenceTests` (conexão real via HubConnection) |
| Isolamento por sessão | `HubGroups`, grupo derivado do token | **I** (isolamento via REST) |
| Eventos servidor → apresentador | `SignalRSessionNotifier` (grupo host) | **I** `HubPresenceTests` (`ParticipantLeft` com total de ativos) |
| Eventos servidor → participante | `SignalRSessionNotifier` (grupo do participante) | **I** (payload aberto sem `isCorrect`) |
| Invocações cliente → servidor | `QuizHub` | **—** |
| Tempo autoritativo do servidor | `SessionBroadcaster.BuildQuestionOpened`, `IClock` | **U** (janela de resposta) |
| Entrega resiliente e consistente | `GetSessionState` (sincronização) | **I** |
| Limites de payload e invocação | `QuizHub.EnsureWithinRateLimit`, `MaximumReceiveMessageSize` | **—** |

## session-reporting

| Requisito | Implementação | Verificação |
|---|---|---|
| Persistência do histórico | `session_answers` + snapshot | **I** `ReportIntegrationTests` |
| Ranking final com pódio | `GetSessionReport` | **U** `ReportTests`, **I** |
| Estatísticas por pergunta | `GetSessionReport` | **U** (distribuição, taxa, sem resposta) |
| Listagem de sessões anteriores | `ListSessions` | **—** |
| Exportação do relatório | `ExportSessionReport` | **U** (escape CSV, JSON, 404), **I** |
| Relatórios restritos ao admin | `Features/Reports/*` com `AdminOnly` | **I** (401 sem token) |

## Bugs encontrados pelos testes de integração

A primeira execução real (2026-09-16) falhou em 25/25 e revelou um defeito de produção que nenhum
teste unitário pegaria:

**`AuthSetup` capturava a configuração de forma eager** —
`configuration.GetSection("Auth").Get<AuthOptions>()` no momento do registro, em vez de resolver
`IOptions<AuthOptions>` pelo DI. Toda requisição autenticada quebrava com
`IDX10703: Cannot create a 'SymmetricSecurityKey', key length is zero`, porque o snapshot era lido
antes de qualquer fonte de configuração adicionada depois. Além de quebrar o host de teste, isso
contornava silenciosamente a validação de `ValidateOnStart`.

O mesmo anti-padrão aparecia em `AddApiCors` e `AddApiRateLimiting`, que ainda chamavam
`services.BuildServiceProvider()` — um segundo container, com duplicação de singletons e a mesma
leitura prematura de configuração. Corrigidos junto:

- JWT: `AddOptions<JwtBearerOptions>(scheme).Configure<IOptions<AuthOptions>>(...)`
- CORS: `AddOptions<AspNetCorsOptions>().Configure<IOptions<CorsOptions>>(...)`
- Rate limiting: limites resolvidos por requisição via `context.RequestServices`
- Recusa do curinga em `AllowedOrigins` movida para a validação de inicialização

## Bug estrutural encontrado em 2026-09-18

O primeiro teste de integração do hub expôs um defeito que atingia **toda** a superfície de tempo
real:

```
Cannot resolve scoped service 'ISessionRepository' from root provider.
   at ... OnConnectedAsync
```

O command bus do FastEndpoints resolve handlers a partir do provider **raiz**. Em requisição HTTP
existe escopo ambiente e tudo funciona; dentro de um hub SignalR não existe, e todo serviço `Scoped`
falhava. Atingia os seis métodos do hub que despacham comando — `SyncState`, `StartSession`,
`AdvanceQuestion`, `EndSession`, `SubmitAnswer` — e o despacho de `CloseQuestionCommand` feito pelo
`QuestionExpiryWorker`.

Passou despercebido por duas razões que se somaram: nenhum teste tocava o hub, e em Produção o
`ValidateScopes` está desligado, então o serviço era resolvido do raiz silenciosamente em vez de
lançar.

**Correção:** `IQuizRepository`, `ISessionRepository` e `SessionBroadcaster` passaram de `Scoped`
para `Singleton`. São stateless sobre o `MongoContext` singleton, cujo `IMongoClient` é thread-safe.

**Lição registrada:** a lacuna de cobertura do hub não era cosmética — escondia um defeito que
tornava inoperante o caminho principal de condução da sessão.

## Lacunas conhecidas

1. **Cobertura parcial do hub SignalR.** `HubPresenceTests` cobre conexão real, grupos e
   `ParticipantLeft`. Ainda sem teste: `QuestionOpened`, `QuestionClosed`, `QuestionResult` e as
   invocações de controle (`StartSession`, `AdvanceQuestion`, `EndSession`, `SubmitAnswer`).
   Essa lacuna custou caro — ver abaixo.
2. **Sessões abandonadas, seed e listagem de sessões** não têm teste automatizado — são caminhos
   simples, mas ficam registrados como não verificados.
3. **Deploy no cluster** (task 8.4) ainda não foi executado: falta o namespace real, o Secret e a
   imagem publicada no registry. A imagem builda localmente e os manifests passam no
   `kubectl apply --dry-run=client`.
