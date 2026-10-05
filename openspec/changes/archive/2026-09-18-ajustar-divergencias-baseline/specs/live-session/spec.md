## ADDED Requirements

### Requirement: Presença do participante na sessão
O sistema SHALL manter, para cada participante, se ele está conectado à sessão no momento.
Um participante SHALL ser considerado **ativo** enquanto tiver ao menos uma conexão de tempo real
aberta; SHALL passar a inativo quando sua última conexão cair; e SHALL voltar a ativo ao reconectar.
A mudança de presença MUST NOT alterar pontuação, apelido ou histórico de respostas.

Hoje o campo existe no modelo mas nunca muda de valor — é essa a divergência que o requisito corrige.

#### Scenario: Participante perde a conexão
- **WHEN** a última conexão de tempo real de um participante cai
- **THEN** ele SHALL ser marcado como inativo
- **AND** sua pontuação acumulada e suas respostas já registradas SHALL permanecer inalteradas

#### Scenario: Participante reconecta
- **WHEN** um participante inativo reconecta com seu token de participante
- **THEN** ele SHALL voltar a ser contado como ativo

#### Scenario: Participante entra pela primeira vez
- **WHEN** um participante entra na sessão por PIN e conecta ao tempo real
- **THEN** ele SHALL ser contado como ativo

### Requirement: Consulta de ranking sob demanda
A API SHALL permitir consultar o ranking corrente de uma sessão a qualquer momento, sem esperar o
próximo evento de atualização. A consulta SHALL estar disponível ao admin e aos participantes
daquela sessão, e SHALL indicar se o ranking é final.

#### Scenario: Consulta durante a sessão
- **WHEN** o admin consulta o ranking de uma sessão em andamento
- **THEN** a API SHALL retornar as posições correntes marcadas como não finais

#### Scenario: Consulta após o encerramento
- **WHEN** o admin consulta o ranking de uma sessão encerrada
- **THEN** a API SHALL retornar o ranking marcado como final

#### Scenario: Participante consulta outra sessão
- **WHEN** um participante consulta o ranking de uma sessão que não é a do seu token
- **THEN** a API SHALL recusar a consulta

## MODIFIED Requirements

### Requirement: Avanço de perguntas controlado pelo apresentador
Somente o admin SHALL avançar para a próxima pergunta. Ao abrir uma pergunta, o servidor SHALL registrar o instante de abertura em UTC e calcular o instante de fechamento a partir do tempo limite da pergunta. Uma pergunta SHALL ser fechada quando o tempo expirar, quando todos os participantes ativos tiverem respondido, ou quando o admin avançar.

Para efeito desta regra, **participante ativo** é o que tem conexão de tempo real aberta, conforme
o requisito "Presença do participante na sessão". Participantes inativos MUST NOT ser considerados
no total exigido para o fechamento antecipado — caso contrário, quem fechou o navegador impediria a
pergunta de fechar antes do tempo.

#### Scenario: Avanço para a próxima pergunta
- **WHEN** o admin avança com a pergunta atual aberta
- **THEN** a pergunta atual SHALL ser fechada, a resposta correta SHALL ser revelada e a próxima pergunta SHALL ser aberta com novos instantes de abertura e fechamento

#### Scenario: Avanço após a última pergunta
- **WHEN** o admin avança estando na última pergunta do quiz
- **THEN** a sessão SHALL passar para `encerrada` e o ranking final SHALL ser consolidado

#### Scenario: Todos responderam antes do tempo
- **WHEN** todos os participantes ativos registram resposta antes do fim do tempo limite
- **THEN** a pergunta SHALL ser fechada imediatamente e a resposta correta SHALL ser revelada

#### Scenario: Participante desconectado não bloqueia o fechamento
- **WHEN** um participante perde a conexão sem responder e todos os demais participantes ativos respondem
- **THEN** a pergunta SHALL ser fechada imediatamente, sem esperar o fim do tempo limite

#### Scenario: Participante conectado que não respondeu segura a pergunta
- **WHEN** um participante ativo ainda não respondeu e o tempo limite não expirou
- **THEN** a pergunta MUST NOT ser fechada antecipadamente
