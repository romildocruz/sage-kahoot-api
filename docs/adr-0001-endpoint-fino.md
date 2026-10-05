# ADR 0001 — Endpoint fino despacha Command/Query; a regra vive no Handler

**Status:** aceito
**Data:** 2026-09-16
**Contexto:** `openspec/changes/archive/2026-09-17-bootstrap-sage-kahoot-api/design.md` — D2 e D3

## Decisão

Usamos FastEndpoints por inteiro (endpoints REPR, NSwag), **mas o `Endpoint` não concentra lógica**.
Todo `HandleAsync` faz exatamente quatro coisas:

1. mapear `Request` → `Command` ou `Query`;
2. despachar pelo command bus (`ExecuteAsync`);
3. traduzir `Result<T>` em resposta HTTP pela extensão compartilhada `ToHttp()`;
4. nada mais.

A regra de negócio vive no `Handler`, em `Sage.Kahoot.Application`.

Isso contraria a letra do `docs/prompt-inicial.md`, que sugere "comandos e queries isolados por
endpoint, sem necessidade de MediatR". A orientação do arquiteto prevalece e está registrada aqui.

## Por quê

O motivo não é preferência de estilo: **o `QuizHub` precisa executar os mesmos casos de uso**.
Iniciar sessão, avançar pergunta, encerrar e responder acontecem tanto por HTTP quanto por SignalR.
Se a lógica morasse dentro do `HandleAsync`, o hub teria que duplicá-la ou chamar HTTP contra a
própria aplicação. Com o command bus, HTTP e SignalR são dois adaptadores finos sobre um handler só.

Há um terceiro chamador que fecha o argumento: o `QuestionExpiryWorker`, que despacha
`CloseQuestionCommand` sem nenhum contexto HTTP.

## Regra de corte da validação

Duas camadas validam, sem sobreposição:

| Onde | O quê | Exemplo |
|---|---|---|
| `Validator<TRequest>` (camada Api) | Forma do request: obrigatoriedade, tamanho, faixa numérica, formato | Título não vazio; tempo limite entre 5 e 120; PIN com 6 dígitos |
| `Handler` (camada Application) | Regras que dependem de estado ou de domínio | Exatamente uma opção correta; PIN existe; apelido disponível; quiz não está em uso |

As faixas numéricas usadas pelos validators vêm das constantes de `QuizRules` e `SessionRules` —
as mesmas que o domínio aplica. Sem isso, borda e domínio divergem quando um limite muda.

## Consequências

- `Sage.Kahoot.Application` depende de `FastEndpoints` (pelo command bus). Acoplamento estreito:
  uma interface e um método por handler. `Sage.Kahoot.Domain` permanece sem dependência externa.
- A biblioteca não tem `IQuery`: queries usam `ICommand<TResult>` e se distinguem por sufixo
  (`...Query` × `...Command`) e pela ausência de efeito colateral. É convenção, não garantia do
  compilador.
- Handlers que despacham sub-comandos (`SubmitAnswerHandler`, `GetSessionStateHandler`) dependem do
  resolvedor estático do FastEndpoints e por isso são cobertos por teste de integração, não unitário.

## Itens fixos de code review

1. Nenhum `HandleAsync` de endpoint nem método de `QuizHub` contém regra de negócio.
2. Nenhuma regra de estado/domínio dentro de um `Validator<TRequest>`.
3. Nenhuma faixa numérica literal em validator — usar as constantes de `QuizRules`/`SessionRules`.
