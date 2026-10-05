## Why

A revisão retroativa do código contra o `docs/prompt-inicial.md` e contra a baseline em
`openspec/specs/` encontrou quatro divergências. Esta change corrige três delas; a quarta virou
change própria.

| # | Divergência | Decisão |
|---|---|---|
| B | `IsConnected` nunca muda: a regra "todos os participantes ativos responderam" não dispara | **B1** — rastrear presença de verdade |
| C | Evento `ParticipantLeft` declarado e documentado, nunca emitido | **C1** — emitir na queda da última conexão |
| D | `GetPartialRankingQuery` implementado e nunca despachado | **D1** — expor por REST e pelo hub |
| A | Banco de perguntas reutilizável nunca implementado | **A1, em change própria** → `adicionar-banco-de-perguntas` |

**B e C compartilham a mesma causa**: o `QuizHub` não rastreia presença — `OnDisconnectedAsync` só
limpa o contador de rate limit. Corrigir presença resolve os dois, e por isso estão no mesmo grupo
de tasks.

O item A foi separado porque sozinho é maior que B, C e D somados: nova coleção, novo repositório,
oito casos de uso, sete endpoints. Mantê-lo aqui travaria três correções pequenas atrás de uma
feature e impediria que qualquer uma das quatro fosse revisada com atenção.

## What Changes

### Presença do participante (B)

`Participant.SetConnected()` existe e ninguém chama; `IsConnected` é sempre `true`. Como
`ActiveParticipants` filtra por esse campo, a regra "todos os participantes ativos responderam →
fecha a pergunta" exige resposta de **todos que já entraram**, inclusive quem fechou o navegador.
Na prática a pergunta quase sempre só fecha por tempo ou por avanço do apresentador.

O `QuizHub` passa a marcar presença nos dois extremos do ciclo de vida da conexão, e a contagem de
ativos passa a refletir quem está de fato conectado. A spec ganha a definição do que torna um
participante ativo — hoje ela usa o termo sem defini-lo.

### Evento de saída (C)

`ParticipantLeft` está declarado em `IQuizClient`, implementado em `SignalRSessionNotifier`, exigido
pela spec `realtime-hub` e documentado em `docs/contrato-hub-signalr.md` — mas nenhum handler o
emite. O frontend pode registrar o handler e esperar para sempre.

Passa a ser emitido quando a **última** conexão do participante cai. Trocar de aba não dispara saída.

### Ranking sob demanda (D)

O handler existe, respeita o desempate correto e nunca é despachado. O ranking chega ao cliente só
por push: um apresentador que recarrega o telão entre perguntas fica sem pódio até o próximo evento.

Passa a ser exposto como `GET /api/v1/sessions/{id}/ranking` e como invocação `GetRanking` no hub,
ambos delegando ao handler existente.

Sem **BREAKING**: as três são aditivas. `ParticipantLeft` passa a cumprir um contrato já publicado,
e quem não registrou handler simplesmente o ignora.

## Capabilities

### Modified Capabilities
- `live-session`: define o que torna um participante "ativo" e o efeito disso no fechamento
  antecipado da pergunta; adiciona a consulta de ranking sob demanda.
- `realtime-hub`: define quando `ParticipantLeft` é emitido, incluindo o caso de múltiplas conexões
  do mesmo participante; adiciona a invocação de ranking.

## Impact

- **Código afetado**: `Api/Hubs/QuizHub.cs`, `Domain/Sessions/Session.cs` e `Participant.cs`,
  `Application/Abstractions/ISessionRepository.cs`, `Infrastructure/Mongo/SessionRepository.cs`,
  novos casos de uso em `Application/Sessions/`, novo endpoint em `Api/Features/Sessions/`.
- **Documentação**: `docs/contrato-hub-signalr.md` (quando `ParticipantLeft` dispara, o que é
  participante ativo, a nova invocação) e `docs/cobertura-specs.md`.
- **Migração de dados**: nenhuma. `IsConnected` já existe no documento de sessão com valor `true`;
  passa a ser mantido a partir das próximas conexões. Sessões antigas seguem legíveis e o relatório
  não muda, pois já recalcula tudo a partir de `session_answers`.
- **Compatibilidade**: nenhuma quebra de contrato. Todas as alterações de API são aditivas.

## Premissas registradas

1. As quatro decisões foram aprovadas: A1 (em change separada), B1, C1 e D1.
2. O item A saiu desta change e vive em `adicionar-banco-de-perguntas`, que tem proposta, specs,
   design e tasks próprios.
3. A Open Question sobre o frontend precisar de ranking sob demanda foi resolvida pela decisão D1;
   a consulta será exposta independentemente de o `sage-kahoot-web` usá-la de imediato.
