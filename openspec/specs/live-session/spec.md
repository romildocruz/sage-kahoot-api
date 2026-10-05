## Purpose

Governa o treinamento ao vivo: a sessão que o apresentador abre a partir de um quiz, o PIN que os participantes usam para entrar, o avanço controlado das perguntas e a pontuação que combina acerto e velocidade.

## Requirements

### Requirement: Abertura de sessão a partir de um quiz
A API SHALL permitir ao admin abrir uma sessão vinculada a um quiz existente. A sessão criada SHALL armazenar uma cópia imutável (snapshot) das perguntas e opções do quiz no momento da abertura, receber um identificador próprio, um PIN e o status inicial `aguardando`.

#### Scenario: Sessão aberta com sucesso
- **WHEN** o admin abre uma sessão para um quiz com pelo menos uma pergunta
- **THEN** a API SHALL retornar HTTP 201 com o identificador da sessão, o PIN gerado e o status `aguardando`

#### Scenario: Quiz sem perguntas
- **WHEN** o admin tenta abrir uma sessão para um quiz sem nenhuma pergunta
- **THEN** a API SHALL retornar HTTP 400 indicando que o quiz precisa ter ao menos uma pergunta

#### Scenario: Snapshot isola a sessão
- **WHEN** o quiz de origem é editado após a abertura da sessão
- **THEN** as perguntas apresentadas na sessão em andamento SHALL permanecer as do snapshot

### Requirement: Geração e unicidade do PIN
O PIN SHALL ser numérico de 6 dígitos, gerado por fonte criptograficamente segura, e SHALL ser único entre todas as sessões não encerradas. Após o encerramento da sessão o PIN SHALL deixar de ser aceito e ficar disponível para reuso em sessões futuras.

#### Scenario: PIN único entre sessões ativas
- **WHEN** o admin abre uma nova sessão enquanto outras sessões ativas existem
- **THEN** o PIN gerado MUST NOT coincidir com o PIN de nenhuma sessão ativa

#### Scenario: PIN de sessão encerrada
- **WHEN** um participante tenta entrar com o PIN de uma sessão já encerrada
- **THEN** a API SHALL retornar HTTP 404 com mensagem de PIN inválido ou sessão indisponível

### Requirement: Entrada anônima do participante
A API SHALL permitir a entrada sem autenticação informando PIN e apelido. O apelido SHALL ter de 2 a 20 caracteres, ser sanitizado contra conteúdo executável e ser único dentro da sessão. Em sucesso, a API SHALL retornar um identificador opaco de participante e um token de participante vinculado àquela sessão, usado para responder e para conectar ao hub.

#### Scenario: Entrada válida
- **WHEN** um participante informa um PIN de sessão em `aguardando` e um apelido disponível
- **THEN** a API SHALL retornar HTTP 200 com o identificador de participante, o token de participante e os dados públicos da sessão

#### Scenario: Apelido já em uso
- **WHEN** um participante informa um apelido já usado por outro participante da mesma sessão
- **THEN** a API SHALL retornar HTTP 409 indicando que o apelido está em uso

#### Scenario: PIN inexistente
- **WHEN** um participante informa um PIN que não corresponde a nenhuma sessão ativa
- **THEN** a API SHALL retornar HTTP 404 com mensagem genérica, sem revelar se existem sessões ativas

#### Scenario: Sessão já iniciada
- **WHEN** um participante tenta entrar em uma sessão cujo status já é `em andamento`
- **THEN** a API SHALL permitir a entrada e o participante SHALL começar com 0 ponto, sem poder responder perguntas já encerradas

### Requirement: Limite de participantes por sessão
A sessão SHALL aceitar no máximo um número configurável de participantes, com padrão de 100, compatível com o porte de uso interno. Tentativas acima do limite SHALL ser rejeitadas.

#### Scenario: Limite atingido
- **WHEN** um participante tenta entrar em uma sessão que já atingiu o limite configurado
- **THEN** a API SHALL retornar HTTP 409 indicando que a sessão está lotada

### Requirement: Estados e transições da sessão
A sessão SHALL ter os estados `aguardando`, `em andamento`, `encerrada` e `cancelada`. As transições permitidas SHALL ser: `aguardando` → `em andamento` (iniciar), `em andamento` → `encerrada` (após a última pergunta ou por encerramento manual), e `aguardando`/`em andamento` → `cancelada`. Qualquer outra transição SHALL ser rejeitada.

#### Scenario: Início da sessão
- **WHEN** o admin inicia uma sessão em `aguardando`
- **THEN** a sessão SHALL passar para `em andamento` e a primeira pergunta SHALL ser aberta

#### Scenario: Transição inválida
- **WHEN** o admin tenta iniciar uma sessão já `encerrada`
- **THEN** a API SHALL retornar HTTP 409 e o estado da sessão MUST NOT mudar

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

### Requirement: Registro de resposta do participante
O participante SHALL poder registrar no máximo uma resposta por pergunta, identificando a opção escolhida. O servidor SHALL aceitar a resposta apenas se a pergunta indicada for a pergunta aberta no momento e se o instante de recebimento estiver dentro da janela de resposta, com tolerância configurável para latência de rede (padrão 500 ms). O tempo de resposta SHALL ser medido pelo relógio do servidor, nunca por valor enviado pelo cliente.

#### Scenario: Resposta válida dentro do tempo
- **WHEN** um participante responde a pergunta aberta dentro da janela de tempo
- **THEN** a API SHALL registrar a opção escolhida, o tempo de resposta em milissegundos e a pontuação obtida

#### Scenario: Segunda resposta para a mesma pergunta
- **WHEN** um participante que já respondeu a pergunta atual envia outra resposta
- **THEN** a API SHALL rejeitar a segunda resposta com HTTP 409 e manter a primeira registrada

#### Scenario: Resposta após o fechamento
- **WHEN** um participante responde depois do instante de fechamento acrescido da tolerância
- **THEN** a resposta SHALL ser rejeitada e SHALL pontuar 0

#### Scenario: Resposta para pergunta que não é a atual
- **WHEN** um participante envia resposta referenciando uma pergunta diferente da aberta
- **THEN** a API SHALL rejeitar a resposta com HTTP 409

### Requirement: Pontuação por acerto e velocidade
Resposta incorreta SHALL pontuar 0. Resposta correta SHALL pontuar entre 50% e 100% da pontuação base da pergunta, decrescendo linearmente com o tempo de resposta: quem responde instantaneamente recebe 100% da base e quem responde no limite do tempo recebe 50%. O valor final SHALL ser arredondado para inteiro. O cálculo SHALL ser determinístico e reproduzível a partir dos dados persistidos.

#### Scenario: Acerto imediato
- **WHEN** um participante acerta uma pergunta de 1000 pontos base com tempo de resposta próximo de zero
- **THEN** a pontuação atribuída SHALL ser aproximadamente 1000

#### Scenario: Acerto no limite do tempo
- **WHEN** um participante acerta uma pergunta de 1000 pontos base respondendo no instante final da janela
- **THEN** a pontuação atribuída SHALL ser aproximadamente 500

#### Scenario: Erro
- **WHEN** um participante escolhe uma opção incorreta
- **THEN** a pontuação atribuída SHALL ser 0, independentemente do tempo de resposta

#### Scenario: Ausência de resposta
- **WHEN** a pergunta é fechada e um participante não respondeu
- **THEN** SHALL ser registrada ausência de resposta com 0 ponto para aquele participante e pergunta

### Requirement: Ranking parcial entre perguntas
Após o fechamento de cada pergunta, o servidor SHALL consolidar o ranking parcial com apelido, pontuação acumulada e posição, ordenado por pontuação decrescente. Empates SHALL ser desempatados pelo menor tempo total de resposta acumulado e, persistindo, pela ordem de entrada na sessão.

#### Scenario: Ranking após fechamento
- **WHEN** uma pergunta é fechada
- **THEN** o ranking parcial SHALL estar disponível com as posições atualizadas de todos os participantes

#### Scenario: Empate em pontuação
- **WHEN** dois participantes têm a mesma pontuação acumulada
- **THEN** SHALL ficar à frente aquele com menor tempo total de resposta acumulado

### Requirement: Reconexão do participante
Um participante que perde a conexão SHALL poder reconectar-se à mesma sessão usando seu token de participante, mantendo apelido, pontuação e histórico de respostas. A reconexão MUST NOT permitir responder perguntas já fechadas.

#### Scenario: Reconexão durante a sessão
- **WHEN** um participante reconecta com token válido enquanto a sessão está `em andamento`
- **THEN** o servidor SHALL restaurar sua identidade e enviar o estado atual da sessão, incluindo sua pontuação acumulada

#### Scenario: Reconexão com token de outra sessão
- **WHEN** um participante tenta reconectar com token emitido para outra sessão
- **THEN** o servidor SHALL rejeitar a reconexão

### Requirement: Sessões abandonadas
Sessões em `aguardando` ou `em andamento` sem qualquer atividade por um período configurável (padrão 4 horas) SHALL ser automaticamente marcadas como `cancelada`, liberando o PIN.

#### Scenario: Sessão ociosa
- **WHEN** uma sessão permanece sem atividade além do período configurado
- **THEN** a sessão SHALL passar para `cancelada` e seu PIN MUST NOT ser mais aceito

### Requirement: Presença do participante na sessão
O sistema SHALL manter, para cada participante, se ele está conectado à sessão no momento.
Um participante SHALL ser considerado **ativo** enquanto tiver ao menos uma conexão de tempo real
aberta; SHALL passar a inativo quando sua última conexão cair; e SHALL voltar a ativo ao reconectar.
A mudança de presença MUST NOT alterar pontuação, apelido ou histórico de respostas.

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
