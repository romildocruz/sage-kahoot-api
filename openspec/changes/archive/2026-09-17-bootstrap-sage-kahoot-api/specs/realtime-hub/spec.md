## Purpose

Define o contrato de tempo real entre o telão do apresentador, os dispositivos dos participantes e o servidor: quais eventos existem, o que carregam e quando são disparados, de modo que o frontend possa ser construído contra um contrato estável.

## ADDED Requirements

### Requirement: Endpoint único de hub em tempo real
A API SHALL expor um hub de tempo real hospedado no mesmo processo e no mesmo deployment da API REST, em uma rota fixa e versionada. O hub SHALL aceitar tanto conexões de apresentador (autenticadas com token de admin) quanto de participante (autenticadas com token de participante).

#### Scenario: Conexão de participante
- **WHEN** um participante conecta ao hub com token de participante válido
- **THEN** o servidor SHALL aceitar a conexão e associá-la à sessão correspondente

#### Scenario: Conexão sem token
- **WHEN** um cliente tenta conectar ao hub sem token de admin nem de participante
- **THEN** o servidor SHALL rejeitar o handshake

### Requirement: Isolamento por sessão
Cada conexão SHALL ser associada exclusivamente à sessão indicada por seu token. Eventos de uma sessão MUST NOT ser entregues a conexões de outra sessão. Uma conexão MUST NOT conseguir entrar em uma sessão indicando manualmente um identificador diferente do contido em seu token.

#### Scenario: Evento restrito à sessão
- **WHEN** uma pergunta é aberta na sessão A com conexões ativas nas sessões A e B
- **THEN** somente as conexões da sessão A SHALL receber o evento

#### Scenario: Tentativa de ingresso em outra sessão
- **WHEN** uma conexão invoca ingresso em uma sessão diferente da vinculada ao seu token
- **THEN** o servidor SHALL rejeitar a invocação

### Requirement: Eventos servidor → apresentador
O servidor SHALL notificar a conexão do apresentador quando: um participante entra (apelido e total de participantes), um participante sai, uma pergunta é aberta (enunciado, opções, tempo limite, instantes de abertura e fechamento, índice e total de perguntas), a contagem de respostas recebidas muda, uma pergunta é fechada (resposta correta, distribuição de respostas por opção), o ranking parcial é atualizado e a sessão é encerrada (ranking final).

#### Scenario: Entrada de participante
- **WHEN** um participante entra na sessão
- **THEN** o apresentador SHALL receber evento com o apelido do participante e o total atualizado de participantes

#### Scenario: Fechamento de pergunta
- **WHEN** a pergunta atual é fechada
- **THEN** o apresentador SHALL receber evento com a opção correta e a quantidade de respostas por opção

### Requirement: Eventos servidor → participante
O servidor SHALL notificar cada participante quando: a sessão inicia, uma pergunta é aberta (opções e tempo restante, **sem** indicação da opção correta), sua própria resposta é confirmada (registrada, sem revelar acerto), a pergunta é fechada (opção correta, se ele acertou, pontos ganhos, pontuação acumulada e posição), e a sessão é encerrada (posição final e pontuação final).

#### Scenario: Pergunta aberta
- **WHEN** o apresentador abre uma pergunta
- **THEN** cada participante SHALL receber o enunciado, as opções e o tempo restante
- **AND** o payload MUST NOT conter qualquer indicação de qual opção é a correta

#### Scenario: Resultado da pergunta
- **WHEN** a pergunta é fechada
- **THEN** cada participante SHALL receber a opção correta, se acertou, os pontos ganhos, a pontuação acumulada e sua posição

### Requirement: Invocações cliente → servidor
O hub SHALL aceitar do apresentador as invocações de iniciar sessão, avançar pergunta e encerrar sessão; e do participante a invocação de enviar resposta e de sincronizar estado. Toda invocação SHALL ser validada quanto a autorização, estado da sessão e idempotência antes de alterar qualquer estado.

#### Scenario: Avanço pelo apresentador
- **WHEN** o apresentador invoca avançar pergunta com a sessão `em andamento`
- **THEN** o servidor SHALL fechar a pergunta atual, abrir a próxima e emitir os eventos correspondentes a apresentador e participantes

#### Scenario: Participante tenta avançar
- **WHEN** uma conexão de participante invoca avançar pergunta
- **THEN** o servidor SHALL rejeitar a invocação e o estado da sessão MUST NOT mudar

#### Scenario: Sincronização de estado
- **WHEN** um cliente invoca sincronizar estado após reconectar
- **THEN** o servidor SHALL responder com o estado atual da sessão, a pergunta aberta (se houver) e o tempo restante calculado pelo relógio do servidor

### Requirement: Tempo autoritativo do servidor
Todos os prazos SHALL ser derivados de instantes UTC emitidos pelo servidor. Os payloads de abertura de pergunta SHALL incluir instante de abertura, instante de fechamento e tempo restante no momento do envio, permitindo ao cliente exibir a contagem regressiva. O servidor MUST NOT aceitar do cliente qualquer valor de tempo decorrido para fins de pontuação.

#### Scenario: Cliente entra com pergunta já aberta
- **WHEN** um participante conecta enquanto uma pergunta está aberta há 7 segundos de um limite de 20
- **THEN** o servidor SHALL informar tempo restante de aproximadamente 13 segundos

#### Scenario: Cliente envia tempo próprio
- **WHEN** uma resposta chega contendo um campo de tempo decorrido informado pelo cliente
- **THEN** o servidor SHALL ignorar esse valor e usar seu próprio relógio para pontuar

### Requirement: Entrega resiliente e consistente
A entrega de eventos SHALL tolerar desconexões momentâneas: um cliente que reconecta SHALL conseguir recompor o estado corrente via sincronização, sem depender de ter recebido eventos anteriores. O estado autoritativo da sessão SHALL residir no servidor, nunca no cliente.

#### Scenario: Reconexão após queda
- **WHEN** um participante perde a conexão durante uma pergunta e reconecta antes do fechamento
- **THEN** ele SHALL receber o estado atual e ainda poder responder, se ainda não tiver respondido e o tempo não tiver expirado

#### Scenario: Divergência de estado do cliente
- **WHEN** o estado exibido no cliente diverge do estado do servidor após reconexão
- **THEN** o estado enviado pelo servidor na sincronização SHALL prevalecer

### Requirement: Limites de payload e invocação
O hub SHALL limitar o tamanho das mensagens recebidas e a taxa de invocações por conexão, com valores configuráveis, rejeitando o excedente sem derrubar a sessão dos demais participantes.

#### Scenario: Invocações em excesso
- **WHEN** uma conexão excede a taxa configurada de invocações
- **THEN** o servidor SHALL rejeitar as invocações excedentes daquela conexão
- **AND** as demais conexões da sessão SHALL continuar operando normalmente
