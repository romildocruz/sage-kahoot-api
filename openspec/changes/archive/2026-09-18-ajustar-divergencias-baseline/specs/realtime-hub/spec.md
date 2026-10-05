## MODIFIED Requirements

### Requirement: Eventos servidor → apresentador
O servidor SHALL notificar a conexão do apresentador quando: um participante entra (apelido e total de participantes), um participante sai, uma pergunta é aberta (enunciado, opções, tempo limite, instantes de abertura e fechamento, índice e total de perguntas), a contagem de respostas recebidas muda, uma pergunta é fechada (resposta correta, distribuição de respostas por opção), o ranking parcial é atualizado e a sessão é encerrada (ranking final).

O evento de saída SHALL ser emitido quando a última conexão de tempo real do participante cair, e
SHALL carregar o apelido e o total atualizado de participantes ativos. O evento MUST NOT ser emitido
quando o participante mantém outra conexão aberta — trocar de aba não é sair.

O evento já era exigido por este requisito e já estava declarado no contrato do hub, mas nenhum
caminho de execução o emitia — é essa a divergência que o requisito fecha.

#### Scenario: Entrada de participante
- **WHEN** um participante entra na sessão
- **THEN** o apresentador SHALL receber evento com o apelido do participante e o total atualizado de participantes

#### Scenario: Saída de participante
- **WHEN** a última conexão de tempo real de um participante cai
- **THEN** o apresentador SHALL receber o evento de saída com o apelido e o total atualizado de participantes ativos

#### Scenario: Participante com conexão remanescente
- **WHEN** uma das conexões de um participante cai e ele ainda mantém outra aberta
- **THEN** o evento de saída MUST NOT ser emitido

#### Scenario: Fechamento de pergunta
- **WHEN** a pergunta atual é fechada
- **THEN** o apresentador SHALL receber evento com a opção correta e a quantidade de respostas por opção

### Requirement: Invocações cliente → servidor
O hub SHALL aceitar do apresentador as invocações de iniciar sessão, avançar pergunta e encerrar sessão; e do participante a invocação de enviar resposta e de sincronizar estado. Ambos SHALL poder invocar a consulta do ranking corrente. Toda invocação SHALL ser validada quanto a autorização, estado da sessão e idempotência antes de alterar qualquer estado.

#### Scenario: Avanço pelo apresentador
- **WHEN** o apresentador invoca avançar pergunta com a sessão `em andamento`
- **THEN** o servidor SHALL fechar a pergunta atual, abrir a próxima e emitir os eventos correspondentes a apresentador e participantes

#### Scenario: Participante tenta avançar
- **WHEN** uma conexão de participante invoca avançar pergunta
- **THEN** o servidor SHALL rejeitar a invocação e o estado da sessão MUST NOT mudar

#### Scenario: Sincronização de estado
- **WHEN** um cliente invoca sincronizar estado após reconectar
- **THEN** o servidor SHALL responder com o estado atual da sessão, a pergunta aberta (se houver) e o tempo restante calculado pelo relógio do servidor

#### Scenario: Consulta de ranking pelo hub
- **WHEN** um cliente autorizado invoca a consulta de ranking da sua sessão
- **THEN** o servidor SHALL responder com as posições correntes e a indicação de que o ranking é final ou parcial
