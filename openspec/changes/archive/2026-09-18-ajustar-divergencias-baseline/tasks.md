> Correção das divergências B, C e D da revisão retroativa. O item A virou a change
> `adicionar-banco-de-perguntas`.
> Os grupos 1 e 2 são independentes entre si e podem ser aplicados em qualquer ordem.
> Referências: `specs/` para o comportamento exigido, `design.md` (D1–D2) para as decisões técnicas.

## 0. Decisões registradas

- [x] 0.1 Item A — implementar o banco de perguntas (A1), em change própria: `adicionar-banco-de-perguntas`
- [x] 0.2 Item B — rastrear presença de verdade (B1)
- [x] 0.3 Item C — emitir `ParticipantLeft` na queda da última conexão (C1)
- [x] 0.4 Item D — expor a consulta de ranking por REST e pelo hub (D1)
- [x] 0.5 Retirar das delta specs as notas condicionais das alternativas recusadas
- [x] 0.6 Mover `specs/question-bank/` e `specs/quiz-management/` para `adicionar-banco-de-perguntas`

## 1. Presença do participante (itens B e C)

- [x] 1.1 Adicionar ao `Session` os métodos de marcar participante como conectado e desconectado, preservando pontuação, apelido e respostas
- [x] 1.2 Adicionar à porta `ISessionRepository` e ao `SessionRepository` a atualização atômica de `participants.$.isConnected`
- [x] 1.3 Criar os casos de uso de conexão e desconexão de participante em `Application/Sessions/`, emitindo `ParticipantLeft` apenas quando a última conexão cair
- [x] 1.4 Implementar no `QuizHub` o contador de conexões por participante e ligar `OnConnectedAsync`/`OnDisconnectedAsync` aos casos de uso (design.md — D1)
- [x] 1.5 Teste unitário: participante desconectado sai da contagem de ativos sem perder pontuação
- [x] 1.6 Teste unitário: participante com segunda conexão aberta não é marcado como inativo nem gera evento de saída
- [x] 1.7 Teste unitário: com um participante desconectado sem responder, a pergunta fecha quando os demais ativos respondem
- [x] 1.8 Teste unitário: com um participante ativo que não respondeu, a pergunta não fecha antecipadamente
- [x] 1.9 Teste unitário: participante inativo volta a ser contado como ativo ao reconectar
- [x] 1.10 Teste de integração do hub: conectar, derrubar a conexão e observar `ParticipantLeft` com o total de ativos atualizado
- [x] 1.11 Atualizar `docs/contrato-hub-signalr.md` descrevendo quando `ParticipantLeft` é emitido, o comportamento com múltiplas conexões e o que significa "participante ativo"

## 2. Consulta de ranking sob demanda (item D)

- [x] 2.1 Criar `Features/Sessions/GetRanking/` expondo `GET /api/v1/sessions/{sessionId}/ranking`, despachando o `GetPartialRankingQuery` já existente
- [x] 2.2 Restringir o acesso: admin em qualquer sessão, participante apenas na sessão do seu token
- [x] 2.3 Adicionar ao `QuizHub` a invocação `GetRanking(sessionId)` com a mesma regra de autorização
- [x] 2.4 Teste de integração: ranking parcial durante a sessão e final após o encerramento
- [x] 2.5 Teste de integração: participante recebe recusa ao consultar o ranking de outra sessão
- [x] 2.6 Atualizar `docs/contrato-hub-signalr.md` com a nova invocação

## 3. Fechamento

- [x] 3.1 Rodar as duas suítes e confirmar que nenhum teste existente regrediu
- [x] 3.2 Revisar que nenhum `HandleAsync` de endpoint nem método de hub ganhou regra de negócio (ADR 0001)
- [x] 3.3 Atualizar `docs/cobertura-specs.md` com os requisitos novos e como cada um é verificado
- [ ] 3.4 Avisar o time do `sage-kahoot-web` que o fechamento antecipado passará a ocorrer com frequência real, e que a contagem regressiva precisa lidar com fechamento a qualquer instante (design.md — riscos)
      <!-- PENDENTE COM O ARQUITETO: o aviso ao time é ação fora do repositório e não pode ser
           executado por aqui. O conteúdo já está redigido em docs/contrato-hub-signalr.md,
           seção "Presença", incluindo o alerta destacado para a UI. -->
- [x] 3.5 Confirmar que as delta specs descrevem exatamente o que foi implementado, antes do `/opsx:archive`
