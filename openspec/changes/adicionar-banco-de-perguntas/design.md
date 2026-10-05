## Context

Ver `proposal.md` — Why. Esta change nasce do item A da revisão retroativa feita em
`ajustar-divergencias-baseline`, cuja decisão foi implementar o banco (A1) em change separada.

Restrições herdadas, todas já vigentes no repositório:

- **Endpoint fino despacha Command/Query** (`docs/adr-0001-endpoint-fino.md`). Vale integralmente
  para os endpoints novos.
- **Regra de corte da validação**: forma do request no `Validator<TRequest>`, estado e domínio no
  handler. As faixas numéricas vêm de `QuizRules`, nunca de literais.
- **Snapshot imutável da sessão**: abrir uma sessão congela o quiz. Nada aqui pode reabrir essa porta.
- Uma réplica, MongoDB fora do cluster, escala de dezenas de participantes por sessão.

## Goals / Non-Goals

**Goals:**
- Reaproveitar conteúdo entre turmas sem duplicar quizzes inteiros.
- Manter o quiz dono das suas perguntas, preservando a garantia de que conteúdo não muda debaixo de
  um treinamento em curso.
- Reaproveitar as invariantes de conteúdo já implementadas, sem duplicar regra de validação.

**Non-Goals:**
- Versionamento ou histórico de alterações das perguntas do acervo.
- Vocabulário controlado de etiquetas, taxonomia ou categorias hierárquicas.
- Importação em massa por arquivo (CSV/JSON) — pode virar change futura.
- Compartilhamento entre usuários ou permissões por acervo: só existe um papel `admin`.

## Decisions

### D1 — Importação por cópia, nunca por referência

A pergunta importada recebe **novos identificadores** e passa a pertencer ao quiz. O quiz não guarda
ponteiro para o item do banco.

Referenciar criaria um terceiro nível de indireção — banco → quiz → snapshot da sessão — e reabriria
exatamente o problema que o snapshot resolveu: editar conteúdo debaixo de um treinamento em curso.
Com referência, corrigir um erro de digitação no acervo mudaria silenciosamente quizzes que já foram
revisados e aprovados por quem vai apresentar.

A mecânica já existe: `Question.CloneWithNewIds()` é usado hoje na duplicação de quiz e serve aqui
sem alteração.

**Consequência assumida:** corrigir uma pergunta no banco não propaga para quizzes montados. É o
custo direto da garantia acima, e a spec o torna explícito em vez de deixar como surpresa. Se um dia
propagação for desejada, ela vira uma operação deliberada ("ressincronizar quiz com o banco"), não um
efeito colateral de editar.

**Alternativa descartada:** referência com resolução na abertura da sessão. Mantém uma fonte única,
mas quebra a previsibilidade do quiz revisado e não elimina a cópia — apenas a adia para o snapshot.

### D2 — Entidade própria, invariantes compartilhadas

`BankQuestion` é uma entidade separada de `Question`, não uma reutilização direta:

```
question_bank
{ _id, text, timeLimitSeconds, basePoints,
  options: [ { id, text, isCorrect } ],
  tags: [ string ],
  createdAtUtc, updatedAtUtc }
```

Índices: `{ updatedAtUtc: -1 }` (listagem padrão), `{ text: "text" }` (busca por enunciado),
`{ tags: 1 }` (filtro por etiqueta).

São entidades distintas porque têm ciclos de vida distintos: `Question` tem `Order` (posição dentro
do quiz) e não tem etiquetas nem carimbo próprio de atualização; `BankQuestion` tem o inverso. Forçar
uma só entidade obrigaria `Order` a ser opcional e sem significado no acervo.

O que **não** se duplica é a regra: as invariantes de conteúdo — 2 a 4 opções distintas, exatamente
uma correta, faixas de tempo e pontuação — ficam em um validador de conteúdo compartilhado, extraído
de `Question.Create`, e as duas entidades o invocam. Sem isso, mudar uma faixa exigiria lembrar de
dois lugares, que é precisamente como a baseline acumulou divergências.

**Alternativa descartada:** guardar perguntas de acervo na coleção `quizzes`, sob um quiz especial
marcado como "banco". Evita a quarta coleção, mas contamina todas as consultas de quiz com um filtro
de exclusão, e uma consulta esquecida faria o acervo aparecer como quiz na tela do apresentador.

### D3 — Superfície REST

| Método | Rota | Observação |
|---|---|---|
| POST | `/api/v1/question-bank` | cadastra pergunta no acervo |
| GET | `/api/v1/question-bank` | paginado; filtros `text` e `tag` |
| GET | `/api/v1/question-bank/{questionId}` | |
| PUT | `/api/v1/question-bank/{questionId}` | |
| DELETE | `/api/v1/question-bank/{questionId}` | não afeta quizzes nem sessões |
| POST | `/api/v1/quizzes/{quizId}/questions/import` | corpo com a lista de ids do banco |
| POST | `/api/v1/quizzes/{quizId}/questions/{questionId}/promote` | do quiz para o acervo |

Todas sob `AdminOnly`. As duas últimas ficam sob `/quizzes` porque o recurso alterado é o quiz — a
importação muda o quiz, não o acervo.

A importação aceita **lista** de ids em vez de um por chamada: montar um quiz a partir do acervo é a
operação central desta change, e fazê-la em N requisições multiplicaria a chance de o quiz ficar
parcialmente montado se uma falhar. Com lista, a operação é toda-ou-nada dentro do agregado.

### D4 — Bloqueio por sessão ativa

Importar e promover seguem a mesma regra já aplicada a editar e excluir quiz: recusado com 409 se o
quiz estiver vinculado a sessão `aguardando` ou `em andamento`. Reaproveita
`ISessionRepository.HasActiveSessionForQuizAsync`, que já existe.

Promover não altera o quiz de origem e, a rigor, poderia ser permitido com sessão ativa. Mantive a
mesma restrição por consistência: duas operações vizinhas sob `/quizzes/{id}/questions/...` com
regras de bloqueio diferentes é o tipo de assimetria que gera bug de expectativa. Se o time preferir
liberar, é um ajuste de uma linha — mas deve ser decisão explícita, não descuido.

## Risks / Trade-offs

- **Divergência entre acervo e quizzes montados (D1)** → Intencional e documentada na spec. O risco
  real é de expectativa, não de dados: alguém corrige o banco e espera ver o quiz mudar. Mitigação:
  a resposta da importação deixa claro que houve cópia, e a spec cobre o cenário explicitamente.
- **Quarta coleção aumenta a superfície de índices** → Três índices novos, criados pelo inicializador
  idempotente existente. Sem migração, sem passo manual.
- **Extrair o validador de conteúdo de `Question.Create` toca código coberto por testes** → É
  refatoração sem mudança de comportamento; os 62 testes unitários existentes são a rede de proteção
  e devem passar sem alteração. Se algum precisar mudar, é sinal de que o comportamento mudou e a
  extração está errada.
- **Acervo cresce sem curadoria** → Busca por enunciado e filtro por etiqueta atendem o porte atual
  (dezenas a centenas de perguntas). Paginação com o mesmo teto de 100 já usado nas demais listagens.
- **Promoção bloqueada por sessão ativa é mais restritiva que o necessário (D4)** → Escolha
  consciente por simetria; registrada aqui para poder ser revista com conhecimento de causa.

## Migration Plan

Nenhuma migração. A coleção `question_bank` nasce vazia, com os índices criados no mesmo
`MongoInitializer` em segundo plano que já cria os das outras três — portanto sem bloquear a
inicialização e sem passo operacional.

Rollback: reverter a imagem. Nada nas coleções `quizzes`, `sessions` e `session_answers` muda de
formato, então a versão anterior continua lendo tudo normalmente. A coleção `question_bank` fica
órfã e inerte, e pode ser removida à mão se o rollback for definitivo.

## Open Questions

Podem ser respondidas depois sem alterar a abordagem:

1. O seed sintético deve popular o acervo com perguntas de exemplo, além do quiz de exemplo que já
   cria? A delta não exige.
2. Excluir uma pergunta do acervo deve ser exclusão física ou lógica? A delta assume física; lógica
   só se passar a existir necessidade de auditoria.
