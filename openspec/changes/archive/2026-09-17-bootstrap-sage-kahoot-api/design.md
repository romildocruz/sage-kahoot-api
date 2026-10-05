## Context

Ver `proposal.md` — Why. Repositório greenfield: hoje contém apenas `docs/` e `openspec/`, sem código, sem git inicializado e sem padrão de projeto estabelecido a seguir (conforme exigido pela política interna, **não há padrão identificável no repositório** — este documento propõe um e aguarda aprovação antes da implementação).

Restrições que moldam o desenho, todas vindas de `docs/prompt-inicial.md`:

- Stack fixada: .NET 9 / C#, **FastEndpoints** (REPR, estilo Minimal API) no lugar de Controllers/MediatR, organização de pastas **por feature/caso de uso**, SignalR **no mesmo processo** da API, MongoDB via `MongoDB.Driver` (sem EF Core), Swagger via o gerador nativo do FastEndpoints (**NSwag**).
- **Instrução explícita do arquiteto, que prevalece sobre a letra do `prompt-inicial.md`:** mesmo com FastEndpoints, o Endpoint **não concentra a lógica** — ele monta e despacha um Command ou Query, que é tratado por um Handler. O arquivo sugere "comandos e queries isolados por endpoint, sem necessidade de MediatR"; a orientação recebida é manter a separação Endpoint → Command/Query → Handler. Ver D3.
- MongoDB roda fora do cluster (instância local ou container Docker no servidor), acessado por connection string configurável.
- Escala modesta: dezenas de participantes por sessão, uso interno, sem exposição pública.
- Um único papel (`admin`), sem cadastro de usuários, sem SSO/AD.
- Placeholders não resolvidos: `{{NAMESPACE_K8S}}`, `{{DOMINIO_INTERNO}}`, `{{REGISTRY_IMAGENS}}`.
- Escopo deste repositório: apenas `sage-kahoot-api`. O `sage-kahoot-web` é projeto separado.

## Goals / Non-Goals

**Goals:**
- Estrutura de solution e camadas que suporte as 6 capabilities de `specs/` sem reorganização posterior.
- Contratos REST e de hub estáveis o suficiente para o time do `sage-kahoot-web` começar a trabalhar contra eles.
- Modelagem MongoDB que sustente as consultas de sessão ao vivo (caminho quente) e de relatório (caminho frio) sem agregações caras em tempo real.
- Endpoints finos e casos de uso reutilizáveis: a mesma lógica servindo HTTP e SignalR sem duplicação.
- Isolamento das decisões substituíveis (mecanismo de token, driver de persistência, transporte de tempo real) atrás de portas próprias.
- Estado autoritativo no servidor, com relógio do servidor como única fonte de verdade para pontuação.

**Non-Goals:**
- Escalonamento horizontal do hub (backplane Redis/Azure SignalR) — instância única basta no porte atual.
- Event sourcing, read models separados fisicamente ou mensageria. CQRS aqui é separação de Commands/Queries no código, não separação de bases.
- Multi-tenancy, múltiplos admins, perfis, auditoria formal.
- Frontend, pipeline de CI/CD e chart Helm completo (manifests K8s simples bastam nesta fase).

## Decisions

### D1 — Estrutura da solution: 4 projetos + 2 de teste, todos sob `src/`

```
sage-kahoot-api/
├─ Sage.Kahoot.sln
├─ src/
│  ├─ Sage.Kahoot.Domain/          # entidades, value objects, regras puras. Sem dependências.
│  ├─ Sage.Kahoot.Application/     # commands, queries, handlers, portas (interfaces), DTOs, validações
│  ├─ Sage.Kahoot.Infrastructure/  # MongoDB, relógio, gerador de PIN, emissor de token, notificador do hub
│  ├─ Sage.Kahoot.Api/             # host ASP.NET: Endpoints (FastEndpoints), Hubs, DI, Swagger, health
│  ├─ Sage.Kahoot.UnitTests/       # domínio e handlers (pontuação, transições, validação)
│  └─ Sage.Kahoot.IntegrationTests/# API + Mongo real via Testcontainers; hub via TestServer
├─ Dockerfile
├─ deploy/k8s/k8sdeploy.yml
└─ docs/
```

Direção das dependências: `Api → Infrastructure → Application → Domain`. `Application` define **portas** (`IQuizRepository`, `ISessionRepository`, `ISessionNotifier`, `IClock`, `IPinGenerator`, `ITokenIssuer`) e `Infrastructure` as implementa; `Api` só compõe. Isso mantém a lógica de pontuação e de transição de estado testável sem Mongo nem SignalR.

**Alternativa descartada:** projeto único com pastas. Descartada porque a direção de dependência deixa de ser verificável pelo compilador — nada impede um handler referenciar `IMongoCollection` diretamente, e o custo de 4 projetos é desprezível.

**Alternativa descartada:** Vertical Slice puro em projeto único, com a fatia inteira (Endpoint + Request + Response + Validator + Handler + acesso a dados) na mesma pasta. É o arranjo mais comum em projetos FastEndpoints e o que o `prompt-inicial.md` sugere ao falar em "cada caso de uso implementado como um Endpoint próprio". Descartado porque dilui a fronteira de persistência e, principalmente, porque a lógica precisa ser invocável também pelo `QuizHub` — o caso de uso não pode viver dentro do endpoint HTTP. O desenho adotado preserva o fatiamento por caso de uso (D2), mas em duas camadas: a fatia HTTP em `Api/Features/` e a fatia de caso de uso em `Application/`.

### D2 — Organização interna por camada

```
Sage.Kahoot.Domain/
├─ Quizzes/            Quiz, Question, AnswerOption, QuizErrors
├─ Sessions/           Session, SessionStatus, Participant, SessionQuestion, Answer, Scoring
└─ Common/             Entity, Result<T>, DomainError, guards

Sage.Kahoot.Application/          # uma pasta por caso de uso: Command/Query + Handler juntos
├─ Abstractions/       portas de domínio (IClock, IPinGenerator, ITokenIssuer, I*Repository, ISessionNotifier)
│                      + middlewares do command bus (logging, validação)
├─ Quizzes/
│  ├─ CreateQuiz/      CreateQuizCommand, CreateQuizHandler
│  ├─ UpdateQuiz/      UpdateQuizCommand, UpdateQuizHandler
│  ├─ DuplicateQuiz/   DuplicateQuizCommand, DuplicateQuizHandler
│  ├─ DeleteQuiz/      DeleteQuizCommand, DeleteQuizHandler
│  ├─ GetQuizById/     GetQuizByIdQuery, GetQuizByIdHandler
│  └─ ListQuizzes/     ListQuizzesQuery, ListQuizzesHandler
├─ Sessions/           OpenSession, JoinSession, StartSession, AdvanceQuestion,
│                      SubmitAnswer, EndSession, GetSessionState, GetPartialRanking
├─ Reporting/          GetSessionReport, ListSessions, ExportSessionReport
└─ Auth/               AdminLogin

Sage.Kahoot.Infrastructure/
├─ Mongo/              MongoContext, QuizRepository, SessionRepository, serializers, índices, MongoOptions
├─ Auth/               JwtTokenIssuer, AdminCredentialValidator, AuthOptions
├─ Time/               SystemClock
├─ Sessions/           SecurePinGenerator, AbandonedSessionSweeper (BackgroundService)
└─ Seed/               SyntheticQuizSeeder

Sage.Kahoot.Api/
├─ Features/           uma pasta por caso de uso HTTP: Endpoint + Request + Response + Validator
│  ├─ Auth/Login/              LoginEndpoint, LoginRequest, LoginResponse, LoginValidator
│  ├─ Quizzes/CreateQuiz/      CreateQuizEndpoint, CreateQuizRequest, QuizResponse, CreateQuizValidator
│  ├─ Quizzes/UpdateQuiz/      …
│  ├─ Quizzes/DuplicateQuiz/   …
│  ├─ Quizzes/DeleteQuiz/      …
│  ├─ Quizzes/GetQuiz/         …
│  ├─ Quizzes/ListQuizzes/     …
│  ├─ Sessions/OpenSession/    …
│  ├─ Sessions/JoinSession/    …
│  ├─ Sessions/StartSession/   …
│  ├─ Sessions/AdvanceQuestion/…
│  ├─ Sessions/EndSession/     …
│  ├─ Sessions/SubmitAnswer/   …
│  ├─ Sessions/GetState/       …
│  └─ Reports/                 GetSessionReport, ListSessions, ExportSessionReport
├─ Hubs/               QuizHub, HubContracts (nomes de métodos e payloads)
├─ Realtime/           SignalRSessionNotifier (implementa a porta ISessionNotifier)
├─ Auth/               políticas, handler do token no hub, extensões de DI
├─ Middleware/         ExceptionHandlingMiddleware, CorrelationIdMiddleware
├─ Configuration/      FastEndpoints, Swagger (NSwag), CORS, RateLimiting, HealthChecks, Serilog, DI
└─ Program.cs
```

O fatiamento por caso de uso acontece nas **duas** camadas e com o mesmo nome em ambas: `Api/Features/Quizzes/CreateQuiz/` (contrato HTTP) e `Application/Quizzes/CreateQuiz/` (caso de uso). Encontrar tudo sobre um caso de uso é abrir duas pastas homônimas, e a fronteira entre transporte e regra continua verificável pelo compilador.

**Dois níveis de validação, sem sobreposição:**
- `Validator<TRequest>` do FastEndpoints (FluentValidation, na camada `Api`): forma do request — obrigatoriedade, tamanho, faixa numérica, formato. Roda antes do handler e produz 400 automaticamente.
- Handler (camada `Application`): regras que dependem de estado ou de domínio — PIN existe, apelido disponível na sessão, quiz não está em uso por sessão ativa, exatamente uma opção correta. Retorna `DomainError`.

### D3 — Endpoint fino + command bus: Endpoint → Command/Query → Handler

**FastEndpoints 8.3.0** (MIT, .NET 8/9/10) é adotado por inteiro: endpoints REPR no lugar de Controllers, NSwag no lugar de Swashbuckle. O command bus (`ICommand<TResult>` / `ICommandHandler<,>`) **já vem no pacote principal** — `FastEndpoints` depende de `FastEndpoints.Messaging` e de `FluentValidation`, então não há pacote extra a instalar para ter CQRS.

**A regra que estrutura tudo:** o `Endpoint` é um adaptador de transporte, nunca o dono da regra. Seu `HandleAsync` faz exatamente quatro coisas — mapear `Request` → `Command`/`Query`, despachar, traduzir `Result<T>` → resposta HTTP, e nada mais. Toda a lógica vive no Handler, em `Application`.

```csharp
// Api/Features/Sessions/SubmitAnswer/SubmitAnswerEndpoint.cs
public sealed class SubmitAnswerEndpoint : Endpoint<SubmitAnswerRequest, AnswerResultResponse>
{
    public override void Configure()
    {
        Post("/sessions/{SessionId}/answers");
        Policies("SessionParticipant");
        Description(b => b.Produces<AnswerResultResponse>(200).ProducesProblemDetails(409));
    }

    public override async Task HandleAsync(SubmitAnswerRequest r, CancellationToken ct)
    {
        var result = await new SubmitAnswerCommand(r.SessionId, User.ParticipantId(), r.QuestionId, r.OptionId)
            .ExecuteAsync(ct);
        await SendResultAsync(result.ToHttp());   // extensão compartilhada: DomainError -> status
    }
}
```

O mesmo `SubmitAnswerCommand` é despachado pelo `QuizHub`. **Esta é a razão de peso para manter a separação** que você pediu: se a lógica morasse dentro do `HandleAsync` do endpoint, o hub teria que duplicá-la ou chamar HTTP contra a própria aplicação. Com o command bus, HTTP e SignalR são dois adaptadores sobre o mesmo caso de uso.

Forma:

- Commands e Queries implementam `ICommand<TResult>`; handlers implementam `ICommandHandler<TCommand, TResult>` com `ExecuteAsync(cmd, ct)`.
- Despacho pela extensão `await new AlgumCommand(...).ExecuteAsync(ct)`, disponível em endpoints, hubs e background services.
- Descoberta automática dos handlers por varredura de assembly — registrar explicitamente o assembly de `Application`.
- Pipeline por `ICommandMiddleware<TCommand, TResult>` em `AddCommandMiddleware(...)`, na ordem: correlação/logging → handler. A validação de request fica no `Validator<TRequest>` do endpoint (D2), antes do despacho.
- **Queries**: a biblioteca expõe apenas `ICommand<TResult>`, sem um `IQuery` distinto. Queries usam a mesma interface, distinguidas por sufixo (`...Query` × `...Command`) e pela ausência de efeito colateral. A separação CQRS passa a ser convenção verificável em code review, não distinção de tipo — trade-off aceito para não manter um segundo barramento só por simetria.

Handlers retornam `Result<T>` (sucesso ou `DomainError` tipado) em vez de lançar exceções para fluxo previsível; uma extensão compartilhada traduz `DomainError` → status HTTP (`NotFound` → 404, `Conflict` → 409, `Validation` → 400), usada por todos os endpoints. Exceções ficam para falhas genuinamente inesperadas, capturadas pelo middleware global.

**Consequência assumida:** `Sage.Kahoot.Application` referencia `FastEndpoints` (pelo command bus) e os handlers implementam uma interface de biblioteca externa. O acoplamento é estreito — uma interface e um método por handler — e o handler continua testável por instanciação direta em teste unitário. As **portas de domínio** (`IQuizRepository`, `ISessionNotifier`, `IClock`, ...) continuam sendo interfaces do próprio projeto; é ali que o isolamento importa. `Domain` permanece sem nenhuma dependência externa.

**Alternativas descartadas:**
- **Lógica dentro do `HandleAsync` do endpoint**, sem command bus — é o que o `prompt-inicial.md` sugere ao dizer "sem necessidade de MediatR", e é o uso idiomático do FastEndpoints. Descartado por instrução explícita do arquiteto e porque o `QuizHub` precisa dos mesmos casos de uso.
- **MediatR** — desde 02/07/2025 é comercial sob a Lucky Penny Software; v12 e anteriores seguem OSS, mas congelar versão para fugir de licença é dívida técnica. Além disso seria um segundo barramento sobre o que o FastEndpoints já traz.
- **Dispatcher próprio** (~150 linhas) — sem razão de existir quando o barramento já vem no pacote que a aplicação usa para o transporte.

### D4 — Modelagem MongoDB: 3 coleções

Documento agregado para quiz; sessão separada das respostas para evitar documento que cresce sem limite.

**`quizzes`** — perguntas embutidas (sempre lidas junto com o quiz, limite natural de dezenas de perguntas, muito abaixo dos 16 MB por documento).
```
{ _id, title, description, questions: [
    { id, order, text, timeLimitSeconds, basePoints,
      options: [ { id, text, isCorrect } ] } ],
  createdAtUtc, updatedAtUtc, isSeed }
```

**`sessions`** — snapshot do quiz no momento da abertura (garante que editar o quiz não muda a sessão em curso, e que o relatório sobrevive à exclusão do quiz) + participantes embutidos (máx. 100) + estado da pergunta corrente.
```
{ _id, pin, quizId, quizSnapshot: { title, questions: [...] },
  status, currentQuestionIndex, currentQuestionOpenedAtUtc, currentQuestionClosesAtUtc,
  participants: [ { id, nickname, joinedAtUtc, totalScore, totalResponseTimeMs, isConnected } ],
  createdAtUtc, startedAtUtc, endedAtUtc, lastActivityAtUtc }
```

**`session_answers`** — uma resposta por documento; volume real da sessão, base das estatísticas por pergunta.
```
{ _id, sessionId, questionId, questionIndex, participantId,
  selectedOptionId, isCorrect, responseTimeMs, pointsAwarded, answeredAtUtc }
```

Índices: `quizzes { updatedAtUtc: -1 }`, `quizzes { title: "text" }`; `sessions { pin: 1 }` **único parcial** sobre status ∈ {`aguardando`, `em andamento`} (garante unicidade do PIN entre sessões ativas e permite reuso após encerrar), `sessions { status: 1, startedAtUtc: -1 }`, `sessions { lastActivityAtUtc: 1 }`; `session_answers { sessionId: 1, questionId: 1 }` e `{ sessionId: 1, participantId: 1, questionId: 1 }` **único** (garante uma resposta por participante por pergunta no nível do banco, não só no aplicativo).

**Alternativa descartada:** respostas embutidas no documento da sessão. Simplifica a leitura, mas 100 participantes × 30 perguntas = 3000 subdocumentos com escrita concorrente no mesmo documento — contenção e crescimento imprevisível. Coleção separada com índice único é mais barata e mais segura.

**Alternativa descartada:** referenciar o quiz na sessão em vez de copiar. Descartada porque violaria os requisitos de imutabilidade da sessão em curso e de preservação do relatório após exclusão do quiz.

**Nota:** pontuação acumulada do participante é desnormalizada em `sessions.participants[].totalScore` e atualizada com operador atômico (`$inc` posicional) no registro da resposta — o ranking parcial entre perguntas sai de uma leitura simples, sem agregação sobre `session_answers` no caminho quente. O relatório final recalcula a partir de `session_answers`, que é a fonte de verdade; divergência indica bug e é detectável.

### D5 — Concorrência do registro de resposta

Dezenas de respostas chegam em rajada nos primeiros segundos de cada pergunta. O registro é feito em duas etapas: (1) `insert` em `session_answers` — o índice único faz a segunda resposta do mesmo participante falhar com erro de chave duplicada, traduzido em 409 sem necessidade de leitura prévia; (2) `$inc` posicional em `sessions.participants[].totalScore`. Sem transação multi-documento: (1) é a fonte de verdade e (2) é cache reconstruível, o que dispensa exigir replica set do MongoDB (a instalação alvo é standalone em container).

O fechamento da pergunta por "todos responderam" é avaliado após cada inserção, comparando contagem de respostas da pergunta com o número de participantes ativos, sob uma atualização condicional do estado da sessão (`currentQuestionIndex` esperado) — garante que apenas um caminho de execução emita o evento de fechamento.

### D6 — Autenticação: JWT simétrico, credencial única, dois tipos de token

Dois tokens, mesmo emissor e mesma chave, distinguidos pela claim de papel:

| Token | Emitido em | Claims | Validade |
|---|---|---|---|
| Admin | login do apresentador | `role=admin`, `jti`, `iss`, `aud`, `exp` | 8h (configurável) |
| Participante | entrada por PIN | `role=participant`, `sessionId`, `participantId`, `nickname` | até o fim da sessão + margem |

Políticas de autorização: `AdminOnly` nas rotas administrativas e nos métodos de controle do hub; `SessionParticipant` nos métodos de resposta do hub. O token do hub chega por `access_token` na query string (padrão do SignalR para WebSocket, que não permite cabeçalho customizado no handshake) e é lido por um `OnMessageReceived` restrito ao caminho do hub.

Senha do admin comparada em tempo constante (`CryptographicOperations.FixedTimeEquals`). Sem hash na credencial configurada — é um segredo único vindo de Secret do K8s, não um banco de senhas; hash aqui só adicionaria cerimônia. Validação de comprimento mínimo da chave de assinatura (≥ 32 bytes para HS256) no startup, com falha rápida.

**Alternativa descartada:** cookie de sessão. O frontend é SPA em outra origem e o hub precisa do mesmo credencial; JWT no `Authorization` simplifica os dois casos.

### D7 — SignalR: um hub, grupo por sessão, notificação via porta

`QuizHub` único em `/hubs/quiz`. Cada conexão entra no grupo `session:{sessionId}` derivado **do token**, nunca de parâmetro do cliente; o apresentador entra também em `session:{sessionId}:host`, o que separa os eventos que revelam a resposta correta dos que vão para os participantes.

Handlers da `Application` não conhecem SignalR: emitem através da porta `ISessionNotifier`, implementada em `Api/Realtime` sobre `IHubContext<QuizHub>`. O adaptador fica na camada `Api`, e não em `Infrastructure`, porque `IHubContext<QuizHub>` nomeia o próprio `QuizHub` — pôr o adaptador em `Infrastructure` exigiria que ela referenciasse `Api`, invertendo a direção de dependência de D1. A porta continua declarada em `Application`; só a implementação muda de lugar. Assim o mesmo comando (`AdvanceQuestion`) é disparável pelo hub ou por um endpoint REST, e os handlers são testáveis com um notificador falso.

Os métodos do hub são finos pelo mesmo motivo que os endpoints (D3): autenticam, montam o comando e o despacham pelo command bus (`ExecuteAsync`). Nenhuma regra de negócio dentro do hub. `QuizHub` e os `Endpoint`s são dois adaptadores de transporte sobre os mesmos handlers.

O fechamento da pergunta por tempo é feito por um temporizador do servidor agendado na abertura; a expiração também é reavaliada de forma defensiva em cada resposta recebida e em cada sincronização, de modo que a correção não dependa exclusivamente do temporizador.

**Alternativa descartada:** polling REST para o tempo real. Simplifica o servidor, mas dezenas de clientes em polling curto geram mais carga e pioram a experiência da contagem regressiva. SignalR já foi definido no prompt.

### D8 — Superfície REST e versionamento

Prefixo `/api/v1` configurado globalmente (`c.Endpoints.RoutePrefix`), com cada `Endpoint` declarando apenas o trecho próprio da rota em `Configure()`. REST para o que é CRUD e relatório; hub para o que é evento de sessão. Comandos de controle da sessão existem em **ambos** (REST e hub) porque compartilham o mesmo handler — o REST serve de fallback e facilita teste manual via Swagger.

| Método | Rota | Auth |
|---|---|---|
| POST | `/api/v1/auth/login` | pública (rate-limited) |
| GET/POST | `/api/v1/quizzes` | admin |
| GET/PUT/DELETE | `/api/v1/quizzes/{id}` | admin |
| POST | `/api/v1/quizzes/{id}/duplicate` | admin |
| POST | `/api/v1/sessions` | admin |
| POST | `/api/v1/sessions/{id}/start` \| `/advance` \| `/end` | admin |
| GET | `/api/v1/sessions/{id}/state` | admin ou participante da sessão |
| POST | `/api/v1/sessions/join` | pública (rate-limited) |
| POST | `/api/v1/sessions/{id}/answers` | participante |
| GET | `/api/v1/reports/sessions` | admin |
| GET | `/api/v1/reports/sessions/{id}` | admin |
| GET | `/api/v1/reports/sessions/{id}/export?format=csv\|json` | admin |
| GET | `/health/live`, `/health/ready` | pública (rede interna) |

### D9 — Plataforma: observabilidade, erros, limites

- **Serilog** com sink de console em JSON compacto, enriquecido com `TraceId`, `Environment` e `Application`; `RequestLoggingMiddleware` para a linha por requisição. Sem sink de arquivo — em K8s o log vai para stdout.
- **ProblemDetails (RFC 7807/9457)** nativo do FastEndpoints via `c.Errors.UseProblemDetails()` — cobre as falhas de `Validator<TRequest>` (400 com lista de campos). O `DomainError` devolvido pelos handlers é traduzido pela extensão compartilhada de D3. Exceções não tratadas caem no middleware global → 500 genérico com `traceId`, sem stack trace.
- **HealthChecks**: `/health/live` self-check; `/health/ready` inclui ping ao MongoDB com timeout curto (probe de readiness não deve segurar o pod). Registrados fora do `RoutePrefix` dos endpoints.
- **Rate limiting** nativo do ASP.NET Core (`FixedWindowLimiter`) particionado por IP, aplicado a `auth/login` e `sessions/join` via `Options(x => x.RequireRateLimiting(...))` no `Configure()` do endpoint. Descartado o `Throttle()` embutido do FastEndpoints, que particiona por cabeçalho enviado pelo cliente — trivial de burlar justamente nos dois endpoints que precisam do limite.
- **CORS** com lista explícita de origens vinda de configuração; `AllowCredentials`; sem curinga.
- **Swagger** via `FastEndpoints.Swagger` (NSwag 14), com `SwaggerDocument()` no registro e `UseSwaggerGen()` no pipeline, declarando o esquema Bearer para autorizar chamadas na UI. Habilitado sempre em Development/Staging; em Production condicionado a `Swagger:Enabled` (padrão `false`).

### D10 — Configuração, Docker e Kubernetes

Configuração por seções tipadas com validação no startup (`ValidateOnStart`): `Mongo`, `Auth`, `Cors`, `Session`, `RateLimiting`, `Swagger`, `Seed`. `appsettings.json` versionado carrega apenas defaults não sensíveis; tudo que é segredo vem de variável de ambiente (`Mongo__ConnectionString`, `Auth__AdminPassword`, `Auth__SigningKey`) mapeada de Secret.

Dockerfile multi-stage (`sdk:9.0` → `aspnet:9.0`), usuário não-root, `ASPNETCORE_URLS=http://+:8080`. Manifests consolidados em **`deploy/k8s/k8sdeploy.yml`** (arquivo único): ConfigMap, Deployment (1 réplica — **obrigatoriamente 1** sem backplane de SignalR, com `Recreate` em vez de `RollingUpdate` para não haver duas instâncias simultâneas), Service (ClusterIP) e Ingress interno com `{{DOMINIO_INTERNO}}`, probes apontando para `/health/live` e `/health/ready`, `requests/limits` modestos.

O **Secret fica em arquivo separado** (`deploy/k8s/secret.example.yml`), criado uma vez: incluí-lo com placeholders no arquivo de manifests faria cada `kubectl apply` sobrescrever as credenciais reais — o `k8sdeploy.yml` precisa ser reaplicável sem efeito colateral. O `.gitignore` recusa `secret.yml` (a cópia preenchida) e mantém apenas o `.example` versionado.

## Risks / Trade-offs

- **Réplica única do pod é ponto único de falha e impede rolling update sem derrubar sessões ativas** → Aceito conscientemente no porte atual; mitigação operacional: fazer deploy fora do horário de treinamento. Caminho de evolução documentado (backplane Redis) sem mudança de contrato, pois o estado já é autoritativo no Mongo, não em memória do processo.
- **`Application` acoplada ao `FastEndpoints` pelo command bus (D3)** → Trade-off aceito: o barramento vem no mesmo pacote do transporte, então não há custo extra de dependência. Mitigação: o acoplamento é uma interface por handler, sem tipos da biblioteca vazando para `Domain`; a lógica de pontuação e de transição de estado vive no domínio e é testável sem o barramento.
- **Distinção Command/Query por convenção, não por tipo (D3)** → A biblioteca não tem `IQuery`. Mitigação: separação por pasta/sufixo, verificada em code review; queries permanecem livres de efeito colateral por disciplina, não por compilador.
- **Dependência de um mantenedor único em peça estrutural (FastEndpoints)** → Risco maior que antes: a biblioteca agora sustenta transporte, validação, OpenAPI e command bus, e não só o barramento. Mitigação parcial: os handlers e o domínio não dependem do modelo de endpoints, então uma eventual migração de transporte reescreveria a pasta `Api/Features/` sem tocar em regra de negócio. Aceito em troca da redução de cerimônia.
- **Dois níveis de validação podem se sobrepor (D2)** → Regra de corte explícita: forma do request no `Validator<TRequest>`, estado e domínio no handler. Sem essa disciplina, a mesma regra passa a viver em dois lugares e diverge.
- **Endpoint fino é disciplina, não algo que o compilador imponha** → Nada impede alguém pôr regra dentro do `HandleAsync`. Mitigação: item fixo de code review e o próprio fato de o `QuizHub` precisar dos mesmos casos de uso, o que torna a violação visível na primeira duplicação.
- **Desnormalização da pontuação em `sessions.participants[]` pode divergir de `session_answers`** → `session_answers` é a fonte de verdade e o relatório final recalcula a partir dela; teste de integração compara os dois ao fim de uma sessão simulada.
- **Ausência de transação multi-documento (D5)** → Escolhido para não exigir replica set no MongoDB standalone do servidor. Consequência: falha entre o insert da resposta e o `$inc` deixa pontuação parcial desatualizada até o recálculo. Detectável e não afeta o relatório final.
- **MongoDB fora do cluster** → Indisponibilidade derruba a readiness da API e a sessão em andamento. Mitigação: `/health/ready` reflete o estado, retry com backoff no driver, e mensagem clara ao apresentador via hub.
- **Swagger exposto em produção interna** → Padrão desabilitado; habilitar é decisão explícita do time, registrada em ConfigMap.
- **Entrada anônima por PIN de 6 dígitos é enumerável** → Rate limiting por IP no join + PIN válido só enquanto a sessão está ativa + mensagem genérica que não confirma existência de sessão. Rede interna reduz bastante a exposição; se o time quiser mais, o próximo passo é bloqueio temporário por IP após N falhas.
- **Apelido livre é vetor de XSS no telão** → Sanitização e limite de tamanho no servidor, no registro; o frontend também não deve renderizar HTML bruto.
- **Relógio do servidor como autoridade com clientes em redes lentas** → Tolerância configurável de 500 ms na janela de resposta; o tempo restante é sempre recalculado pelo servidor na sincronização.
- **Testcontainers exige Docker na máquina de build** → Testes de integração isolados em projeto próprio e marcados por categoria, para que os testes unitários rodem sem Docker.

## Migration Plan

Greenfield — não há migração de dados nem de consumidores. Sequência de implantação:

1. Provisionar/confirmar o MongoDB acessível a partir do cluster e criar o banco `sage_kahoot`.
2. Criar Secret com `Mongo__ConnectionString`, `Auth__AdminPassword` e `Auth__SigningKey` no namespace `{{NAMESPACE_K8S}}` (valores reais fornecidos pela infraestrutura, nunca versionados).
3. Publicar a imagem em `{{REGISTRY_IMAGENS}}` e aplicar os manifests.
4. Verificar `/health/ready`, executar o seed sintético e validar um fluxo completo de sessão com dois participantes de teste.

Rollback: reaplicar a imagem anterior (`kubectl rollout undo`). Como os índices são criados de forma idempotente no startup e não há migração destrutiva de schema, o rollback não requer intervenção no banco. Sessões ativas no momento do rollback são perdidas — deploy deve ocorrer fora do horário de treinamento.

## Open Questions

Podem ser respondidas depois sem alterar specs, abordagem ou tasks:

1. Valores de `{{NAMESPACE_K8S}}`, `{{DOMINIO_INTERNO}}` e `{{REGISTRY_IMAGENS}}` — permanecem placeholders nos manifests até a infraestrutura informar.
2. Existe chart-base/Helm padrão no cluster a ser seguido? Se sim, os manifests desta change viram valores daquele chart.
3. Padrão de Ingress interno da empresa (classe, anotações, TLS interno).
4. Swagger fica exposto na produção interna? Padrão adotado: desabilitado.
5. Política de retenção do histórico de sessões (hoje: retenção indefinida, volume desprezível).
