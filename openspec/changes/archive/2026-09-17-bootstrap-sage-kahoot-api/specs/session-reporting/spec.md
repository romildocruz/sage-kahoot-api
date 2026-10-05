## Purpose

Entrega ao facilitador o resultado do treinamento depois que a sessão acaba: pódio, desempenho individual por apelido, taxa de acerto por pergunta e exportação simples para consolidar com o RH ou com a liderança da turma.

## ADDED Requirements

### Requirement: Persistência do histórico de sessões
Ao encerrar uma sessão, o sistema SHALL persistir de forma durável: identificador da sessão, referência e snapshot do quiz aplicado, instantes de início e fim em UTC, lista de participantes com apelido e pontuação final, e todas as respostas registradas com opção escolhida, acerto, tempo de resposta e pontos obtidos. Esses dados SHALL permanecer consultáveis independentemente de alterações ou exclusão posterior do quiz de origem.

#### Scenario: Sessão encerrada
- **WHEN** uma sessão passa para `encerrada`
- **THEN** o histórico completo SHALL estar disponível para consulta pelo admin

#### Scenario: Sessão cancelada
- **WHEN** uma sessão é `cancelada`
- **THEN** o histórico SHALL registrar o cancelamento e as respostas coletadas até aquele ponto, marcando a sessão como não concluída

### Requirement: Ranking final com pódio
A API SHALL disponibilizar ao admin o ranking final de uma sessão encerrada, ordenado por pontuação decrescente, contendo posição, apelido, pontuação total, número de acertos, número de erros, número de perguntas não respondidas e tempo médio de resposta. Os três primeiros colocados SHALL ser identificáveis como pódio.

#### Scenario: Consulta do ranking final
- **WHEN** o admin consulta o ranking de uma sessão encerrada
- **THEN** a API SHALL retornar a lista ordenada com todos os participantes e suas estatísticas individuais

#### Scenario: Sessão sem participantes
- **WHEN** o admin consulta o ranking de uma sessão encerrada em que ninguém entrou
- **THEN** a API SHALL retornar HTTP 200 com ranking vazio, sem erro

#### Scenario: Ranking de sessão em andamento
- **WHEN** o admin consulta o ranking de uma sessão ainda `em andamento`
- **THEN** a API SHALL retornar o ranking parcial daquele momento, marcado como não final

### Requirement: Estatísticas por pergunta
A API SHALL disponibilizar, para cada pergunta da sessão, o número de respostas por opção, a taxa de acerto, o tempo médio de resposta e o número de participantes que não responderam. Essas estatísticas permitem ao facilitador identificar os temas com maior dificuldade na turma.

#### Scenario: Consulta de desempenho por pergunta
- **WHEN** o admin consulta o relatório de uma sessão encerrada
- **THEN** a API SHALL retornar, por pergunta, a distribuição de respostas por opção, a taxa de acerto e o tempo médio de resposta

#### Scenario: Pergunta sem nenhuma resposta
- **WHEN** nenhuma resposta foi registrada para uma pergunta da sessão
- **THEN** a taxa de acerto SHALL ser reportada como 0 e a contagem de não respondidas SHALL igualar o número de participantes ativos

### Requirement: Listagem de sessões anteriores
A API SHALL permitir ao admin listar sessões anteriores com paginação, ordenadas por data de início decrescente, exibindo título do quiz, data, status, número de participantes e pontuação máxima atingida, com filtro por intervalo de datas e por status.

#### Scenario: Listagem por intervalo
- **WHEN** o admin lista sessões filtrando por um intervalo de datas
- **THEN** a API SHALL retornar apenas as sessões iniciadas dentro do intervalo, com os dados de paginação

### Requirement: Exportação do relatório
A API SHALL permitir ao admin exportar o relatório de uma sessão em CSV e em JSON. O CSV SHALL conter uma linha por participante com posição, apelido, pontuação, acertos, erros, não respondidas e tempo médio, usando codificação UTF-8 e cabeçalho na primeira linha. O JSON SHALL conter o relatório completo, incluindo as estatísticas por pergunta. O nome do arquivo SHALL identificar a sessão e sua data.

#### Scenario: Exportação em CSV
- **WHEN** o admin solicita a exportação em CSV de uma sessão encerrada
- **THEN** a API SHALL retornar o arquivo com `Content-Type` de CSV, cabeçalho de download e uma linha por participante

#### Scenario: Exportação em JSON
- **WHEN** o admin solicita a exportação em JSON de uma sessão encerrada
- **THEN** a API SHALL retornar o relatório completo com ranking e estatísticas por pergunta

#### Scenario: Apelido com separador do CSV
- **WHEN** um apelido contém vírgula, aspas ou quebra de linha
- **THEN** o valor SHALL ser corretamente escapado no CSV, preservando a integridade das colunas

#### Scenario: Exportação de sessão inexistente
- **WHEN** o admin solicita a exportação de uma sessão cujo identificador não existe
- **THEN** a API SHALL retornar HTTP 404

### Requirement: Relatórios restritos ao admin
Todas as rotas de histórico, ranking final, estatísticas e exportação SHALL exigir token de admin válido.

#### Scenario: Acesso sem token
- **WHEN** um cliente sem token solicita o relatório de uma sessão
- **THEN** a API SHALL retornar HTTP 401 e nenhum dado de sessão SHALL ser retornado
