# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

O código, comentários, XML docs e nomes de teste deste repositório estão em pt-BR. Mantenha esse padrão.

## Comandos

```bash
dotnet build Sage.Kahoot.sln                   # TreatWarningsAsErrors=true: warning quebra o build
dotnet run --project src/Sage.Kahoot.Api       # Swagger em http://localhost:5000/swagger

dotnet test src/Sage.Kahoot.UnitTests          # domínio e handlers, sem dependência externa
dotnet test src/Sage.Kahoot.IntegrationTests   # exige Docker (Testcontainers sobe um mongo:7.0)

# Um teste específico (os nomes são frases em pt-BR com underscore)
dotnet test src/Sage.Kahoot.UnitTests --filter "FullyQualifiedName~Sessao_completa"
dotnet test src/Sage.Kahoot.UnitTests --filter "FullyQualifiedName~QuizHandlerTests"
```

Rodar a API localmente exige um MongoDB de pé (`docker run -d --name sage-kahoot-mongo -p 27017:27017 mongo:7.0`)
e os segredos em user-secrets — `Mongo:ConnectionString`, `Auth:AdminUsername`, `Auth:AdminPassword`,
`Auth:SigningKey` (mínimo 32 bytes). Sem eles o processo sai com código 78 e a mensagem nomeia a chave
faltante. O passo a passo completo, o fluxo de autorização no Swagger e o deploy em k8s estão no `README.md`.

Fluxo de mudança: este projeto usa OpenSpec (`openspec/`, skills `/opsx:*`). Propostas, specs de
comportamento e tasks vivem em `openspec/changes/<nome>/`; as specs consolidadas em `openspec/specs/`.

## Arquitetura

`Api → Infrastructure → Application → Domain`. O compilador garante a direção; `Domain` não tem
dependência externa nenhuma.

### O command bus é o centro — leia `docs/adr-0001-endpoint-fino.md` antes de mexer em endpoint

Todo caso de uso é um `Command`/`Query` do FastEndpoints despachado por `ExecuteAsync`, com o handler
em `Sage.Kahoot.Application`. Isso existe porque **três adaptadores diferentes executam os mesmos casos
de uso**: os endpoints HTTP (`Api/Features/`), o `QuizHub` do SignalR (`Api/Hubs/QuizHub.cs`) e os
workers em segundo plano (`Infrastructure/Sessions/QuestionExpiryWorker`, `AbandonedSessionSweeper`).
Nenhum `HandleAsync` de endpoint nem método do hub contém regra de negócio — mapeia, despacha, traduz.

Não existe `IQuery`: queries são `ICommand<TResult>` e se distinguem só pelo sufixo `...Query` e pela
ausência de efeito colateral. Convenção, não garantia do compilador.

`Program.cs` registra os **dois** assemblies no `AddFastEndpoints` (endpoints na Api, handlers na
Application) — um handler novo em outro assembly não seria encontrado.

### Regra de corte da validação (duas camadas, sem sobreposição)

| Onde | O quê |
|---|---|
| `Validator<TRequest>` (FluentValidation, camada Api) | forma do request: obrigatoriedade, tamanho, faixa numérica, formato |
| `Handler` (camada Application) | regras que dependem de estado ou de domínio |

As faixas numéricas dos validators **sempre** vêm das constantes de `QuizRules`/`SessionRules` —
nunca literais —, porque são as mesmas que o domínio aplica.

### Erros são valores, não exceções

Falha previsível de regra é `Result<T>` com `DomainError` (`Code`, `Message`, `ErrorCategory`).
Exceção fica reservada ao inesperado. A tradução para HTTP é centralizada em
`Api/Common/ResultExtensions.cs` (`ToHttp`/`ToCreated`/`ToNoContent` → ProblemDetails); o `QuizHub`
converte a mesma falha em `HubException`. Os códigos de erro ficam agrupados em `Domain/Sessions/SessionErrors.cs` e
`Application/Quizzes/QuizContracts.cs` (`QuizErrors`), não espalhados pelos handlers.

### Tempo real

Handlers não conhecem SignalR: emitem pela porta `ISessionNotifier` (Application/Abstractions), cuja
implementação é `Api/Realtime/SignalRSessionNotifier`. Os payloads são montados por
`Application/Sessions/SessionBroadcaster` — compartilhado pelos três caminhos que fecham pergunta
(avanço do apresentador, "todos responderam", expiração) para que os três emitam exatamente os mesmos
eventos. Contrato para o frontend: `docs/contrato-hub-signalr.md`. `QuestionOpened` nunca carrega a
opção correta.

### Concorrência e fonte de verdade

- O relógio do servidor (`IClock`) é a única autoridade de tempo; nenhum valor de tempo do cliente
  entra na pontuação. O `participantId` vem do token, nunca do corpo.
- `AnswerRecord` é a fonte de verdade (índice único no Mongo rejeita resposta duplicada); a pontuação
  acumulada do participante é cache reconstruível, atualizado por incremento atômico.
- `ISessionRepository.TryCloseCurrentQuestionAsync` e `ReplaceIfCurrentQuestionAsync` retornam bool:
  só quem vence a corrida emite os eventos de fechamento. Escrita de presença é posicional
  (`SetParticipantConnectedAsync`) — um `ReplaceOne` sobrescreveria pontuações gravadas no intervalo.

## Armadilhas registradas

**Repositórios e serviços do fluxo de comando são `Singleton`, não `Scoped`.** O command bus do
FastEndpoints resolve handlers a partir do provider raiz; `Scoped` quebra todo despacho feito fora de
uma requisição HTTP (hub e workers). Ver o comentário em `InfrastructureServiceCollectionExtensions`.

**Nunca leia `IConfiguration` no registro de serviços** — nem `GetSection().Get<T>()` dentro de uma
extensão de `IServiceCollection`, nem `services.BuildServiceProvider()`. Isso congela a configuração
antes das fontes adicionadas depois (é o que o `WebApplicationFactory` faz para injetar a connection
string do contêiner) e contorna o `ValidateOnStart`. Já quebrou 25/25 testes de integração com
`IDX10703`. Use `IOptions<T>` pelo DI; para opções de framework,
`AddOptions<TFramework>().Configure<IOptions<TNosso>>(...)`. Políticas por requisição (rate limiting)
resolvem de `context.RequestServices`.

**Deploy derruba sessões ao vivo.** SignalR sem backplane ⇒ Deployment fixo em 1 réplica com
estratégia `Recreate`. Não existe rolling update aqui.

## Convenções

- Uma pasta por caso de uso, espelhada nas duas camadas: `Application/Sessions/SubmitAnswer/SubmitAnswerCommand.cs`
  (record do comando + handler no mesmo arquivo) e `Api/Features/Sessions/SubmitAnswer/SubmitAnswerEndpoint.cs`.
  Requests e validators compartilhados ficam no nível acima (`Features/Quizzes/QuizRequests.cs`).
- Injeção por construtor primário; `sealed` por padrão; namespace file-scoped; campo privado `_camelCase`
  (ver `.editorconfig`).
- Testes: xUnit, sem biblioteca de mock — os dublês são classes explícitas em
  `UnitTests/Fakes.cs` (`FixedClock`, `RecordingNotifier`, `InMemory*Repository`). Integração usa
  `ApiFixture` (`WebApplicationFactory` + Testcontainers, um database por instância) e a
  `[Collection(ApiCollection.Name)]`; chame `WaitUntilReadyAsync()` antes de exercitar a API.
- Handlers que despacham sub-comandos (`SubmitAnswerHandler`, `GetSessionStateHandler`) dependem do
  resolvedor estático do FastEndpoints e por isso são cobertos por teste de integração, não unitário.
- Dados de exemplo e de teste são sempre sintéticos; nenhum dado pessoal real entra no repositório.

## Itens fixos de code review (ADR 0001)

1. Nenhum `HandleAsync` de endpoint nem método de `QuizHub` contém regra de negócio.
2. Nenhuma regra de estado/domínio dentro de um `Validator<TRequest>`.
3. Nenhuma faixa numérica literal em validator — usar as constantes de `QuizRules`/`SessionRules`.

## Convenções específicas do projeto (não inferíveis só pelo código)

### Padrão arquitetural
- API em .NET 10 usando **FastEndpoints** (estilo Minimal API) — cada caso de uso é
  um Endpoint próprio (Request/Response/Validator), organizado por feature. Não usar
  MediatR nem Controllers tradicionais.
- SignalR Hub roda **no mesmo processo** da API (sem serviço separado).
- Persistência via **MongoDB.Driver** direto (sem EF Core, sem ORM relacional).

### Autenticação
- Apenas um papel: **admin**. Login único via credencial fixa (Secret/variável de
  ambiente), sem cadastro de usuários, sem múltiplos perfis, sem AD/SSO.
- Endpoints administrativos (CRUD de quiz, controle de sessão) exigem o token emitido
  no login. Participantes não autenticam — acesso via PIN de sessão.

### MongoDB
- Roda **fora do Kubernetes**, como container Docker standalone (ou instância local)
  — não deployar Mongo dentro do cluster. Connection string via ConfigMap/Secret.

### Contrato público (REST + SignalR)
- Swagger/OpenAPI é gerado via **NSwag** (nativo do FastEndpoints).
- O projeto **sage-kahoot-web** (pasta irmã em `C:\dev\sage-kahoot-web`) consome este
  contrato através de um snapshot `openapi.json` exportado manualmente do Swagger.
- **Sempre que uma mudança alterar o contrato público** (novo endpoint, payload
  alterado, mudança em método/evento do SignalR Hub, mudança na autenticação):
  1. Sinalizar explicitamente que a change afeta o contrato público.
  2. Lembrar que o `openapi.json` em `sage-kahoot-web` precisa ser re-exportado e o
     client tipado regenerado lá.
  3. Métodos/eventos do SignalR Hub **não aparecem no OpenAPI** — se mudarem, isso
     precisa ser comunicado à parte (o sage-kahoot-web lê o código-fonte do Hub
     diretamente quando necessário).
- Mudanças puramente internas (refactor, otimização de query, log) não exigem
  nenhuma ação no sage-kahoot-web.

### OpenSpec
- O projeto já tem uma baseline de specs criada retroativamente a partir do código
  existente. Toda mudança nova deve seguir o fluxo normal: proposta de change →
  aprovação → aplicação. Achados de inconsistência com o que foi pedido devem ser
  reportados antes de corrigidos, nunca corrigidos por conta própria.

### Ambiente / Kubernetes local
- Deploy no namespace `sage-kahoot` (Rancher Desktop / Kubernetes local).
- Para pausar a aplicação sem apagar a configuração: escalar Deployment para
  `replicas: 0` (não usar `kubectl delete`).
- Placeholders ainda pendentes de preenchimento: `{{NAMESPACE_K8S}}`,
  `{{DOMINIO_INTERNO}}`, `{{REGISTRY_IMAGENS}}`.
- Sem credenciais reais em nenhum arquivo — sempre usar placeholders e Secrets do
  Kubernetes.