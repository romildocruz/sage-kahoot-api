# Contrato do hub SignalR — sage-kahoot-api

Referência para o time do `sage-kahoot-web`. Este documento descreve o que o servidor envia e o que
aceita receber. Spec de origem: `openspec/changes/archive/2026-09-17-bootstrap-sage-kahoot-api/specs/realtime-hub/spec.md`.

## Conexão

| Item | Valor |
|---|---|
| Rota | `/hubs/quiz` |
| Autenticação | JWT na query string: `?access_token=<token>` |
| Token do apresentador | obtido em `POST /api/v1/auth/login` |
| Token do participante | obtido em `POST /api/v1/sessions/join` |

O token na query string é aceito **apenas** neste caminho — WebSocket não permite cabeçalho
customizado no handshake. Conexão sem token é recusada.

Depois de conectar, invoque `JoinSessionGroup(sessionId)`:

- **Participante**: o `sessionId` precisa ser o mesmo do token, senão a invocação é recusada. Ele já
  entra automaticamente nos grupos da sessão e do próprio participante em `OnConnectedAsync`.
- **Apresentador**: o token de admin não é vinculado a uma sessão (ele autoriza conduzir qualquer
  uma), então o `sessionId` vem por parâmetro. Entra também no grupo de host.

## Servidor → cliente

Registre estes handlers. Os marcados como **host** só chegam à conexão do apresentador.

| Método | Destino | Payload |
|---|---|---|
| `ParticipantJoined` | todos | `{ sessionId, nickname, totalParticipants }` |
| `ParticipantLeft` | todos | `{ sessionId, nickname, totalParticipants }` — ver "Presença" abaixo |
| `SessionStarted` | todos | `{ sessionId, totalQuestions }` |
| `QuestionOpened` | todos | `{ sessionId, questionId, questionIndex, totalQuestions, text, options[{id,text}], timeLimitSeconds, openedAtUtc, closesAtUtc, remainingSeconds }` |
| `AnswerCountChanged` | **host** | `{ sessionId, questionId, answeredCount, activeParticipants }` |
| `AnswerAccepted` | participante | `{ sessionId, questionId }` |
| `QuestionClosed` | **host** | `{ sessionId, questionId, correctOptionId, distribution[{optionId,count}] }` |
| `QuestionResult` | participante | `{ sessionId, questionId, correctOptionId, wasCorrect, pointsEarned, totalScore, position }` |
| `RankingUpdated` | todos | `{ sessionId, entries[{position,participantId,nickname,totalScore}], isFinal }` |
| `SessionEnded` | **host** | mesmo payload de `RankingUpdated`, com `isFinal: true` |
| `ParticipantSessionEnded` | participante | `{ sessionId, position, totalScore }` |

**`QuestionOpened` nunca carrega a opção correta.** Ela só aparece em `QuestionClosed` (host) e em
`QuestionResult` (participante), depois do fechamento.

## Cliente → servidor

| Invocação | Quem pode | Retorno |
|---|---|---|
| `JoinSessionGroup(sessionId)` | ambos | — |
| `SyncState(sessionId)` | ambos | `SessionStatePayload` |
| `StartSession(sessionId)` | apresentador | `SessionDto` |
| `AdvanceQuestion(sessionId)` | apresentador | `SessionDto` |
| `EndSession(sessionId)` | apresentador | `SessionDto` |
| `SubmitAnswer(sessionId, questionId, optionId)` | participante | `AnswerAcceptedResult` |
| `GetRanking(sessionId)` | ambos | `RankingPayload` |

`GetRanking` devolve o ranking corrente sob demanda, sem esperar o próximo `RankingUpdated` — útil
ao recarregar o telão entre perguntas, quando `SyncState` só traz o estado da pergunta. Participante
só consulta a sessão do seu próprio token. Equivalente REST: `GET /api/v1/sessions/{id}/ranking`.

Falha previsível de regra chega como `HubException` com a mensagem de negócio (por exemplo,
"Este participante já respondeu a esta pergunta."). Invocação de método de apresentador por uma
conexão sem papel `admin` é recusada e não altera o estado da sessão.

## Presença: quem conta como participante ativo

Um participante é **ativo** enquanto tiver ao menos uma conexão aberta com o hub. A presença é
rastreada nos dois extremos do ciclo de vida da conexão e tem efeito direto na sessão:

- **Conectar** marca o participante como ativo, inclusive em reconexão.
- **Cair a última conexão** marca como inativo e emite `ParticipantLeft` com o total atualizado de
  participantes **ativos** — não o total de quem já entrou.
- **Fechar uma aba entre duas abertas não emite `ParticipantLeft`.** O servidor conta as conexões por
  participante; trocar de aba não é sair.

Perder a conexão **não** apaga nada: apelido, pontuação acumulada e respostas já registradas
permanecem, e reconectar devolve o participante à contagem de ativos.

O motivo de isso importar está na regra de fechamento: a pergunta fecha antecipadamente quando
**todos os ativos** respondem. Quem fechou o navegador não segura mais a turma esperando o tempo
expirar.

> **Atenção para a UI:** com presença funcionando, o fechamento antecipado passa a ocorrer com
> frequência real. A contagem regressiva precisa lidar com `QuestionClosed` chegando a qualquer
> instante, inclusive poucos segundos após a abertura — não assuma que ela sempre vai até o fim.

## Tempo e reconexão

O relógio do servidor é a única autoridade. `QuestionOpened` traz `openedAtUtc`, `closesAtUtc` e
`remainingSeconds` — use `remainingSeconds` para iniciar a contagem regressiva local. Qualquer campo
de tempo decorrido enviado pelo cliente é ignorado no cálculo da pontuação.

Ao reconectar, chame `SyncState(sessionId)` e **substitua** o estado local pelo que vier:

```
{
  sessionId, status, currentQuestionIndex, totalQuestions,
  currentQuestion: QuestionOpened | null,
  participantCount, myTotalScore, myAnswerRegistered
}
```

`currentQuestion` vem `null` quando não há pergunta aberta — inclusive entre perguntas, após o
fechamento. `myAnswerRegistered` evita que o participante reenvie uma resposta já contabilizada.

## Limites

- Mensagem recebida: 32 KB.
- Invocações por conexão: 60 a cada 10 segundos. O excedente é recusado apenas naquela conexão; as
  demais da sessão seguem normalmente.

## Equivalência REST

Todo comando de controle existe também em REST (`design.md` — D8), compartilhando o mesmo handler.
Serve de alternativa e facilita o teste manual pelo Swagger.
