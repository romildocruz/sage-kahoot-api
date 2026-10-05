## Context

Ver `proposal.md` — Why. A baseline em `openspec/specs/` foi criada retroativamente a partir de uma
implementação que já existia, e por isso ficou silenciosa sobre o que foi construído pela metade.
Esta change fecha três desses silêncios.

Restrições que moldam as decisões:

- O contrato do hub **já foi publicado** ao time do `sage-kahoot-web` em `docs/contrato-hub-signalr.md`.
  Nada aqui pode quebrá-lo — e de fato tudo é aditivo.
- A aplicação roda com **1 réplica obrigatória**, sem backplane de SignalR. Isso simplifica o
  rastreamento de presença: o contador pode viver no processo, com a persistência servindo de registro.
- Decisões vigentes não mudam: Endpoint fino despacha Command/Query
  (`docs/adr-0001-endpoint-fino.md`), snapshot imutável do quiz na sessão, `session_answers` como
  fonte de verdade do relatório.

## Goals / Non-Goals

**Goals:**
- Fechar a distância entre o que a baseline afirma e o que o código faz, sem inventar comportamento novo.
- Manter cada item aplicável isoladamente.
- Não quebrar o contrato já publicado ao frontend.

**Non-Goals:**
- Revisar decisões de arquitetura já aprovadas.
- Banco de perguntas reutilizável — vive em `adicionar-banco-de-perguntas`.
- Escalar o hub para múltiplas réplicas.

## Decisions

### D1 — Presença: conexão como fonte, sessão como registro (itens B e C)

O `QuizHub` passa a chamar o domínio nos dois extremos do ciclo de vida da conexão:

- `OnConnectedAsync` — marca o participante do token como conectado.
- `OnDisconnectedAsync` — marca como desconectado **apenas quando não resta nenhuma outra conexão
  daquele participante**, e só então emite `ParticipantLeft`.

A contagem de conexões por participante é necessária porque o mesmo participante pode abrir duas
abas; tratar a primeira queda como saída produziria evento falso e tiraria alguém ativo da contagem,
fazendo a pergunta fechar cedo demais. Como só existe uma réplica, um contador em memória
(`ConcurrentDictionary<Guid, int>`) resolve, com a persistência de `IsConnected` servindo de registro
para a lógica de fechamento.

**Consequência assumida:** se o processo reiniciar com participantes conectados, `IsConnected` fica
obsoleto até cada um reconectar. O custo real é uma pergunta que espera o tempo expirar em vez de
fechar antes — o mesmo comportamento de hoje. O `AbandonedSessionSweeper`, que já existe, continua
cobrindo sessões que ficam órfãs.

**Alternativa descartada — ajustar a spec ao código** (remover `IsConnected` e falar apenas em
"participantes"): mais barata, mas o fechamento antecipado deixaria de funcionar em qualquer sessão
onde alguém saiu, que é o caso comum em treinamento. Pioraria a experiência de forma silenciosa.

**Alternativa descartada — remover o evento `ParticipantLeft`**: exigiria alterar `IQuizClient`, a
spec `realtime-hub` e a documentação já entregue ao frontend. Só faria sentido junto com a anterior,
pois sem presença não há como saber que alguém saiu.

### D2 — Ranking sob demanda (item D)

`GetPartialRankingQuery` já existe, já respeita o desempate correto (pontuação, tempo total, ordem de
entrada) e já é exercitado indiretamente por teste. Expor custa um endpoint fino e um método de hub,
ambos delegando ao handler existente:

| Superfície | Rota / método | Autorização |
|---|---|---|
| REST | `GET /api/v1/sessions/{sessionId}/ranking` | admin em qualquer sessão; participante só na do seu token |
| Hub | `GetRanking(sessionId)` | mesma regra |

O ganho concreto é para o frontend: hoje, um apresentador que recarrega o telão entre perguntas fica
sem ranking até o próximo push, porque `GetSessionState` devolve estado da pergunta, não o pódio.

**Alternativa descartada — remover o handler**: elimina ~40 linhas. Seria defensável se o frontend
comprovadamente não precisasse de consulta sob demanda, mas expor é barato e reversível, enquanto
apagar exige reescrever depois.

### D3 — Lifetime de DI dos repositórios (descoberto durante a implementação)

Decisão tomada em 2026-09-18, com aprovação explícita, depois que o primeiro teste de integração do
hub falhou com:

```
Cannot resolve scoped service 'ISessionRepository' from root provider.
   at ... OnConnectedAsync
```

O command bus do FastEndpoints resolve handlers a partir do provider **raiz**. Em requisição HTTP
existe um escopo ambiente e tudo funciona; dentro de um hub SignalR não existe escopo algum, e
qualquer dependência `Scoped` falha na resolução.

O alcance não era desta change: atingia os seis métodos do hub que despacham comando — `SyncState`,
`StartSession`, `AdvanceQuestion`, `EndSession`, `SubmitAnswer` — e o despacho de
`CloseQuestionCommand` feito pelo `QuestionExpiryWorker`, ou seja, o fechamento de pergunta por
expiração de tempo.

**Correção:** `IQuizRepository`, `ISessionRepository` e `SessionBroadcaster` passaram de `Scoped`
para `Singleton`. São stateless sobre o `MongoContext` singleton, cujo `IMongoClient` é thread-safe,
então a mudança é segura e vale para qualquer chamador fora de contexto HTTP — presente ou futuro.

**Por que não passou despercebido antes:** em Produção o `ValidateScopes` está desligado, e o
serviço `Scoped` era resolvido do raiz em silêncio, virando singleton de fato. O erro só aparece em
Development — que é exatamente onde os testes rodam.

**Alternativa descartada — criar escopo por despacho no hub e no worker:** mais idiomático em DI,
mas o `ExecuteAsync()` estático do FastEndpoints não aceita escopo; seria preciso contornar o bus e
resolver handlers à mão, furando o ADR 0001. A documentação da biblioteca não cobre execução fora
de contexto HTTP.

**Consequência a vigiar:** se algum repositório passar a guardar estado por requisição, o singleton
deixa de ser seguro. Hoje nenhum guarda, e a revisão deve rejeitar quem tentar.

## Risks / Trade-offs

- **Contador de conexões em memória perde estado no restart (D1)** → Participantes aparecem ativos
  até reconectarem; a pergunta fecha por tempo. É o comportamento atual, não uma regressão. Só
  deixaria de ser aceitável com múltiplas réplicas, que já estão fora de escopo.
- **`ParticipantLeft` passa a ser emitido onde antes havia silêncio** → É evento que o contrato já
  prometia; quem não registrou handler ignora. Aditivo na prática.
- **Duas abas do mesmo participante** → Tratado pelo contador. Sem ele, fechar uma aba emitiria saída
  falsa e distorceria a contagem de ativos — que é justamente o que alimenta o fechamento antecipado.
- **Persistir presença adiciona uma escrita por conexão e desconexão** → Uma atualização posicional
  por evento, em sessões de dezenas de participantes. Desprezível no porte atual.
- **Repositórios como singleton (D3)** → Seguro enquanto forem stateless. Um campo de instância com
  estado por requisição passaria a ser compartilhado entre chamadas concorrentes; é item de revisão.
- **Fechamento antecipado passa a acontecer de fato** → É a correção pretendida, mas muda o ritmo
  percebido da sessão: perguntas passarão a fechar antes do tempo com mais frequência do que hoje.
  Vale avisar o time do `sage-kahoot-web`, cuja UI de contagem regressiva precisa lidar com
  fechamento a qualquer instante — algo que a spec já exigia e que a prática raramente exercitou.

## Migration Plan

Nenhuma migração de dados. `IsConnected` já existe no documento de sessão com valor `true` para todos
os participantes; ao entrar em vigor o rastreamento, o valor passa a ser mantido a partir das próximas
conexões. Documentos antigos permanecem legíveis e o relatório não muda, pois já recalcula tudo a
partir de `session_answers`.

Rollback: reverter a imagem. Nenhuma alteração escreve dados em formato novo, então a versão anterior
continua lendo tudo. Sessões em andamento no momento do rollback são perdidas de qualquer forma, pelo
deploy com estratégia `Recreate` já documentado.

## Open Questions

Podem ser respondidas depois sem alterar a abordagem:

1. O `sage-kahoot-web` deve exibir participantes inativos no telão, esmaecidos, ou ocultá-los? A API
   passa a fornecer a informação; a apresentação é decisão do frontend.
2. Deve existir um período de tolerância antes de marcar alguém como inativo, para absorver quedas
   momentâneas de rede? A delta assume marcação imediata; tolerância pode ser acrescentada depois
   sem mudar contrato.
