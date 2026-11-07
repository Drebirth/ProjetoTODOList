# Autenticação e listas de TODO

## Objetivo

Transformar a API ASP.NET Core existente em uma API de listas pessoais de tarefas. Cada usuário deve conseguir autenticar-se com username e senha e administrar somente as próprias tarefas, com paginação limitada a 15 itens por página.

## Estado atual observado

- A API tem `AuthController`, `TodosController` e `TodoSeriesController`, com serviços de autenticação, JWT, CRUD de tarefas e gerenciamento de recorrência. `RecurrenceWorker` roda como serviço hosted e `RecurrenceService` processa vencimentos e catch-up.
- EF Core com SQLite persiste `User`, `TodoItem`, `RecurrenceSeries` e `Categoria`; migrations incluem autenticação/listas, conclusão, recorrência, título/categoria/prioridade, `20261107170415_AddSchedulingWindowsAndTimeZones` e `20261107171051_AddScheduleAnchorOccurrenceNumber`. A API aplica migrations na inicialização e usa `TodoDatabase`, padrão `Data Source=todoapi.db`.
- `TodoItem` contém título, descrição, categoria/prioridade, datas de criação/conclusão/prazo, janela opcional UTC `DataInicioUtc`/`DataFimUtc`, status, usuário e número/referência de ocorrência. `RecurrenceSeries` contém configuração/cursor de recorrência e âncora local com número da ocorrência/horários de parede para janelas agendadas.
- `TodoService` implementa CRUD isolado pelo usuário, paginação, conclusão, configuração da série, criação/catch-up de ocorrências e exclusão por ocorrência/série; `DELETE /api/todo-series/{seriesId}` cancela série sem precisar de ocorrência.
- Os contratos incluem `Titulo`, `CategoriaId`/`CategoriaNome`, `Prioridade`, `DataConclusaoUtc`, `DataPrevistaUtc` e os metadados de recorrência; `CategoriaService`/`CategoriasController` implementam CRUD de categorias. `TodoService` e `RecurrenceService` gerenciam tarefas/ocorrências.
- Há projeto de testes em `TodoAPI.Tests/`.
- `User.TimeZoneId` guarda o fuso IANA (default `America/Fortaleza`); `PreferencesController` expõe a preferência autenticada. `SchedulingService` valida janelas, exige que início/fim caiam na mesma data local, converte instantes para UTC, projeta slots de séries com janela-base e verifica conflitos.

## Requisitos confirmados

### Usuários e acesso

- O acesso usa username e senha; e-mail não é necessário.
- Cada username deve ser único.
- Cada usuário possui sua própria coleção de tarefas TODO.
- Tarefas de um usuário só podem ser consultadas ou alteradas pelo usuário dono. A regra também se aplica a leitura, atualização e exclusão por ID; não basta ocultar tarefas apenas na listagem.
- A aplicação deve persistir dados localmente usando SQLite.

### Tarefas

- A API deve oferecer CRUD completo para tarefas: criar, listar, consultar, atualizar e excluir.
- Cada tarefa pertence a exatamente um usuário e contém:
  - `Id`;
  - `Descricao`, com no máximo 200 caracteres;
  - data de criação;
  - booleano que indica se foi concluída.
- Cada tarefa registra a data/hora em que foi concluída. O instante é definido pelo servidor em UTC ao marcá-la como concluída e fica nulo quando a tarefa não está concluída.
- Uma tarefa recorrente usa frequência diária, semanal ou mensal e informa a duração como quantidade de períodos na unidade correspondente: dias, semanas ou meses.
- Ao concluir uma tarefa recorrente, deve surgir uma nova ocorrência distinta; a tarefa concluída permanece concluída. A frequência e a duração definem a configuração do ciclo.
- A listagem de tarefas deve ser paginada e retornar no máximo 15 tarefas por página.

### Modelo de conclusão aprovado

- Adicionar `DataConclusaoUtc` nullable ao TODO. Ao criar uma tarefa já concluída ou alterar `Concluido` de `false` para `true`, atribuir o instante UTC atual no servidor. Ao desmarcar (`Concluido=false`), definir `DataConclusaoUtc=null`. Se permanecer concluída durante uma atualização, preservar a data já gravada. O cliente não pode definir esse instante diretamente.

### Recorrência aprovada

- A frequência aceita `Diaria`, `Semanal` ou `Mensal`. A duração é um inteiro obrigatório quando a frequência está definida, com limites 1–3650 (diária), 1–520 (semanal) ou 1–120 (mensal), contando a ocorrência inicial; ambas são nulas para tarefa sem recorrência.
- `DuracaoRecorrencia` representa o número total de períodos da série, incluindo a ocorrência inicial. A unidade é dias para frequência diária, semanas para semanal e meses para mensal. Exemplo: duração `3` gera no máximo três ocorrências no total, contando a primeira.
- A primeira ocorrência de uma série recorrente vence no instante UTC em que ela é criada: `DataPrevistaUtc = DataCriacao`.
- A agenda fica ancorada no prazo da ocorrência anterior. O prazo seguinte é calculado a partir dele: `+1 dia` para diária, `+7 dias` para semanal ou `+1 mês` para mensal. Se o mês seguinte não tiver o mesmo dia do mês, usar seu último dia (clamp), preservando o horário UTC.
- Adicionar `DataPrevistaUtc` nullable, em UTC e definida pelo servidor. Recorrências têm prazo para cada ocorrência; tarefas sem recorrência mantêm o campo nulo. O cliente não pode definir esse campo diretamente.
- Ao concluir uma ocorrência recorrente antes do prazo, manter a ocorrência concluída com `DataConclusaoUtc` e criar imediatamente a próxima ocorrência, com prazo calculado para o período seguinte a partir do prazo ancorado.
- Criar uma tarefa recorrente já com `Concluido=true` também cria imediatamente a ocorrência sucessora, se ainda houver ocorrências dentro do total permitido. A tarefa inicial permanece concluída.
- Se `DataPrevistaUtc` chegar e a ocorrência continuar não concluída, mantê-la pendente e criar a próxima ocorrência quando virar o período. Não reabrir nem apagar ocorrências anteriores.
- Desmarcar uma ocorrência antiga depois que sua sucessora foi criada limpa a conclusão/data da ocorrência antiga, mas não cancela nem remove a sucessora.
- Concluir novamente uma ocorrência já concluída é idempotente: preservar `DataConclusaoUtc` e não criar outra sucessora duplicada.
- Alterar `FrequenciaRecorrencia` ou `DuracaoRecorrencia` atualiza a configuração da série para as próximas ocorrências. Ocorrências já existentes e seus `DataPrevistaUtc` não são alterados; a nova configuração vale para ocorrências futuras ainda não criadas.
- Excluir uma ocorrência recorrente e encerrar uma série são ações distintas, selecionadas explicitamente no contrato HTTP.
- Adicionar `SerieRecorrenciaId` e `NumeroOcorrencia` às ocorrências recorrentes. A primeira ocorrência é número `1`; ocorrências subsequentes incrementam o número. Para tarefas sem recorrência, esses campos ficam nulos.
- Garantir unicidade de `(SerieRecorrenciaId, NumeroOcorrencia)` no banco para impedir duplicatas, inclusive durante catch-up após reinício.
- Uma rotina hosted deve processar vencimentos e, após reinício ou indisponibilidade, criar cada ocorrência de período vencido que ainda caiba no total definido por `DuracaoRecorrencia` (incluindo a inicial).

### Proposta técnica de persistência da recorrência — revisar com Geto

Os padrões abaixo são uma proposta de modelagem para revisão de Geto. Mantenha as regras de negócio aprovadas; Geto deve conferir a consistência destes detalhes de persistência antes de Gojo usá-los como desenho final.

- Criar entidade/tabela `RecurrenceSeries` como fonte de verdade da série, contendo `UserId`, descrição base para novas ocorrências, frequência, duração total, estado ativo, próximo número de ocorrência e próxima `DataPrevistaUtc`. A série não depende da existência de `TodoItem` associados e permanece persistida mesmo quando todas as ocorrências são excluídas individualmente.
- Cada `TodoItem` recorrente referencia a série por `SerieRecorrenciaId` e persiste `NumeroOcorrencia` e sua `DataPrevistaUtc`. Frequência/duração apresentadas pela API vêm da série. Tarefas sem recorrência não têm referência de série.
- Cada resposta de ocorrência expõe `SerieRecorrenciaId`; respostas de tarefas avulsas apresentam `null`. Ao gerar uma nova ocorrência, copiar `UserId` e descrição base da série.
- Garantir índice único no banco em `(SerieRecorrenciaId, NumeroOcorrencia)`.
- Manter na série o próximo número e o próximo prazo mesmo após excluir fisicamente uma ocorrência. O cursor é monotônico e nunca reutiliza número removido; a exclusão de ocorrência apaga apenas a linha de `TodoItem` e deixa o cursor/agendamento da série ativo.
- Excluir série em `scope=series` remove a entidade da série e todas as ocorrências relacionadas por cascade, encerrando o scheduler para aquela série. Como o ID de uma ocorrência deixa de estar disponível após apagar todas individualmente, expor também `DELETE /api/todo-series/{seriesId}` para cancelar diretamente a série, mesmo sem itens associados; a operação valida o `UserId` autenticado.
- Atualizar frequência/duração altera os dados na série para as próximas gerações e preserva ocorrências já criadas e seus prazos. Rejeitar com `400 ProblemDetails` redução da duração total para valor inferior ao maior número de ocorrência já gerado; permitir definir o total igual ao número já gerado para terminar a série sem novas ocorrências.
- Converter tarefa independente em recorrente cria uma série ativa e vincula a tarefa existente como ocorrência número `1`, vencendo no instante UTC da conversão; número seguinte é `2` e próximo prazo da série é calculado conforme frequência a partir desse vencimento.
- Enviar frequência e duração nulas em atualização desativa a geração futura na série, limpa a configuração de recorrência e preserva os TODOs já criados; não os exclui.
- Ao editar a descrição de uma ocorrência recorrente, atualizar também a descrição base da série para as próximas ocorrências; não modificar descrições de ocorrências já existentes. Esse é um detalhe proposto para validação de Geto.

## Nova funcionalidade solicitada: título, categoria e prioridade

### Requisitos confirmados pelo usuário

- Adicionar `Titulo` string obrigatório a `TodoItem`, com máximo de 200 caracteres. É um campo adicional a `Descricao`, que continua conforme o contrato atual.
- Adicionar entidade `Categoria` relacionada a `TodoItem` por navegação/FK e permitir criar, listar, consultar, editar e excluir categorias.
- Categorias pertencem ao usuário; o `CategoriaId` de um TODO deve ser de propriedade do usuário autenticado. Categoria é opcional.
- Criar para cada usuário as categorias iniciais `Estudos`, `Reunião`, `Trabalho` e `Pessoal`.
- Adicionar `Prioridade` com valores `Baixa`, `Media` e `Alta`; quando omitida, usar `Media`.
- Não permitir excluir categoria usada por TODO ou série recorrente; retornar `409 ProblemDetails`.

### Regras para recorrência

- Persistir `Titulo`, `CategoriaId` e `Prioridade` em cada `TodoItem` como valores da ocorrência.
- Como `RecurrenceSeries` precisa gerar tarefas futuras mesmo após a exclusão de todas as ocorrências, manter também `TituloBase`, `CategoriaIdBase` e `PrioridadeBase` na série, junto à `DescricaoBase`. `CategoriaIdBase` deve referenciar categoria pertencente ao mesmo usuário da série.
- Nova ocorrência copia `TituloBase`, `DescricaoBase`, `CategoriaIdBase`, `PrioridadeBase` e `UserId` da série. A categoria base precisa continuar pertencendo ao mesmo usuário.
- Editar título, descrição, categoria ou prioridade de uma ocorrência recorrente atualiza essa ocorrência e os campos-base da série para futuras ocorrências. Itens já criados e seus prazos não são alterados.
- Categoria indicada por `CategoriaIdBase` continua em uso enquanto referenciada pela série (inclusive se nenhuma ocorrência existir); impedir exclusão com `409`.

### Contrato HTTP

Endpoints de categoria protegidos por JWT Bearer. O corpo de criação/edição de categoria contém `nome`; proprietário é sempre derivado da identidade autenticada.

| Operação | Rota | Contrato |
| --- | --- | --- |
| Listar categorias | `GET /api/categories` | Retorna somente categorias do usuário autenticado. |
| Consultar categoria | `GET /api/categories/{id}` | Categoria do usuário autenticado; ausente ou de outro usuário `404`. |
| Criar categoria | `POST /api/categories` | Corpo `nome`; sucesso `201`. |
| Editar categoria | `PUT /api/categories/{id}` | Corpo `nome`; sucesso `200`, ausente ou de outro usuário `404`. |
| Excluir categoria | `DELETE /api/categories/{id}` | Sucesso `204`; se referenciada por TODO ou série, `409 ProblemDetails`. |
| Criar/editar TODO | `POST /api/todos` e `PUT /api/todos/{id}` | Acrescentar `titulo` obrigatório (máximo 200), `categoriaId` nullable e `prioridade` (padrão `Media`). Resposta inclui `Titulo`, `CategoriaId`, categoria relacionada quando atribuída e `Prioridade`. |

O tamanho máximo e a regra de unicidade do nome da categoria não foram definidos pelo usuário; Geto deve revisar se é necessário fixar esses limites antes da implementação.

### Critérios de aceite

1. TodoItem persiste `Titulo` obrigatório de até 200 caracteres; título ausente, branco ou acima do limite retorna `400 ProblemDetails`.
2. `Descricao` existente continua preservada como campo separado.
3. CRUD de categorias opera somente sobre categorias do usuário autenticado; IDs de outro usuário não podem ser lidos ou editados.
4. TODO aceita categoria ausente; se informada, sua categoria pertence ao usuário dono do TODO.
5. Cadastro de usuário cria as categorias iniciais `Estudos`, `Reunião`, `Trabalho` e `Pessoal` para esse usuário.
6. `Prioridade` aceita somente `Baixa`, `Media` ou `Alta`; omissão usa `Media`.
7. Exclusão de categoria usada por ocorrência ou série retorna `409`; categoria sem referência pode ser excluída.
8. Ocorrências recorrentes novas usam título, descrição, categoria, prioridade e proprietário da série; edição atualiza valores-base somente para futuras ocorrências e preserva as já criadas.

## Contrato HTTP aprovado

As rotas abaixo são o contrato da API. Rotas de autenticação são públicas; todas as rotas `/api/todos`, `/api/todo-series`, `/api/categories` e `/api/preferences` exigem JWT Bearer válido.

| Operação | Rota | Observação |
| --- | --- | --- |
| Registrar usuário | `POST /api/auth/register` | Corpo: `username`, `password`. Sem e-mail. Sucesso `201`; username já usado `409`. |
| Login | `POST /api/auth/login` | Corpo: `username`, `password`. Sucesso `200` com token JWT Bearer; credenciais inválidas `401`. |
| Criar tarefa | `POST /api/todos` | Corpo: campos TODO existentes, mais `dataInicioUtc`/`dataFimUtc` opcionais como par de timestamps com offset. Valores são normalizados para UTC; limites devem ser da mesma data local no fuso da conta. Validação `400`; conflito de agenda `409 ProblemDetails`; sucesso `201`. |
| Listar tarefas | `GET /api/todos?page=1&pageSize=15` | `page` padrão 1; `pageSize` padrão 15 e máximo 15. Consulta somente tarefas do usuário autenticado. Sucesso `200`. |
| Consultar tarefa | `GET /api/todos/{id}` | Somente tarefa do usuário autenticado. Inclui título/descrição, categoria/prioridade, conclusão/prazo, `DataInicioUtc`/`DataFimUtc`, configuração e IDs de recorrência. Ausente ou de outro usuário: `404`. Sucesso `200`. |
| Atualizar tarefa | `PUT /api/todos/{id}` | Recebe os campos mutáveis, incluindo `dataInicioUtc`/`dataFimUtc` opcionais como par. Janela parcial, fim não posterior ou limites em datas locais diferentes retorna `400 ValidationProblemDetails`; colisão com agenda retorna `409 ProblemDetails` sem persistência parcial. Em recorrência, a janela editada na ocorrência N vira âncora N para slots futuros; janelas já geradas não mudam. |
| Consultar preferência de fuso | `GET /api/preferences` | Preferência do usuário autenticado; se não houver valor diferente configurado, retorna `America/Fortaleza`. Sucesso `200`. |
| Atualizar preferência de fuso | `PUT /api/preferences` | Corpo `timeZoneId` IANA. Sucesso `200`; valor inválido `400 ValidationProblemDetails`; agenda projetada conflitante `409 ProblemDetails`, sem salvar o fuso novo. |
| Excluir tarefa não recorrente | `DELETE /api/todos/{id}` | Exclusão comum do único item; não requer `scope`. Ausente ou de outro usuário: `404`. Sucesso `204`. |
| Excluir ocorrência recorrente | `DELETE /api/todos/{id}?scope=occurrence` | Exclui somente a ocorrência indicada. A série continua; não recria o número removido e mantém a agenda dos próximos períodos. Sucesso `204`. |
| Excluir série recorrente | `DELETE /api/todos/{id}?scope=series` | Exclui todas as ocorrências da série e a própria série por cascade, encerrando seu agendamento. Sucesso `204`. |
| Cancelar série recorrente | `DELETE /api/todo-series/{seriesId}` | Cancela e exclui a série e todas as ocorrências por cascade; funciona mesmo quando nenhuma ocorrência permanece. Só o usuário dono pode cancelar. Ausente ou de outro usuário: `404`. Sucesso `204`. |

Em ocorrências recorrentes, `scope` é obrigatório e aceita somente `occurrence` ou `series`; ausência ou valor inválido retorna `400 ProblemDetails`. `scope=series` usa a série do ID informado e verifica que ela pertence ao usuário autenticado.

As respostas de TODO, inclusive itens dentro de `TodoPageResponse`, incluem `Id`, `Titulo`, `Descricao`, `CategoriaId`, `CategoriaNome` (nullable), `Prioridade`, `DataCriacao`, `Concluido`, `DataConclusaoUtc`, `DataPrevistaUtc`, `DataInicioUtc`, `DataFimUtc`, `FrequenciaRecorrencia`, `DuracaoRecorrencia`, `SerieRecorrenciaId` e `NumeroOcorrencia`. Frequência e duração são projetadas da entidade `RecurrenceSeries`; datas são serializadas em UTC e datas/campos opcionais são nullable conforme as regras acima.

Respostas de erro seguem `ProblemDetails`. Dados inválidos, incluindo parâmetros de paginação inválidos e descrição inválida, retornam `400`. O contrato de sucesso usa os status HTTP indicados na tabela.

## Segurança e validação

- Não persistir senha em texto puro; armazenar hash seguro para senha.
- Username é único sem diferenciar maiúsculas de minúsculas; por exemplo, `Ana` e `ana` representam o mesmo username. A restrição deve ser garantida também na persistência.
- Descrição é obrigatória, não pode ser vazia nem composta apenas de espaços e não pode exceder 200 caracteres. Violações retornam `400 ProblemDetails`.
- As rotas de tarefas devem exigir autenticação. A identidade do proprietário deve vir do contexto autenticado.
- A autorização deve ocorrer no acesso ao registro no banco para impedir IDOR, tanto em consultas quanto em atualizações e exclusões.
- Não incluir username/senha em respostas de tarefa ou logs.
- `DataCriacao` é definida pelo servidor em UTC no momento da criação e não pode ser alterada pelo cliente. `Concluido` inicia `false` quando omitido na criação.
- `DataConclusaoUtc` é definida pelo servidor em UTC ao criar uma tarefa concluída ou ao concluí-la, preservada se permanecer concluída e limpa para `null` quando ela é desmarcada; não aceitar um valor de conclusão enviado pelo cliente.
- `FrequenciaRecorrencia` aceita `Diaria`, `Semanal` ou `Mensal`. Se informada, `DuracaoRecorrencia` é obrigatória e limitada a 1–3650 para diária, 1–520 para semanal ou 1–120 para mensal; unidade é dia, semana ou mês, incluindo a ocorrência inicial. Duração sem frequência é inválida.
- `DuracaoRecorrencia` é o total de períodos da série incluindo a ocorrência inicial; não gerar mais ocorrências que esse total.
- Primeira ocorrência recorrente vence no instante de criação UTC. Os prazos seguintes são calculados a partir do vencimento anterior: mais um dia, sete dias ou um mês conforme frequência; ao faltar o dia equivalente no mês seguinte, usar o último dia daquele mês, mantendo o horário UTC.
- `DataPrevistaUtc` é nullable, controlada pelo servidor e expressa em UTC; tarefas recorrentes têm uma data prevista e tarefas sem recorrência não têm.
- `SerieRecorrenciaId` e `NumeroOcorrencia` identificam a série e a posição da ocorrência. O par é único no banco; a primeira posição é `1`.
- Ao concluir antecipadamente, criar a próxima ocorrência imediatamente com prazo no próximo período. Ao vencer sem conclusão, manter a ocorrência não concluída e criar a próxima ao virar o período.
- Criar uma ocorrência recorrente já concluída também cria imediatamente sua sucessora se houver limite restante. Desmarcar uma ocorrência antiga não cancela sucessoras já criadas.
- Concluir novamente a mesma ocorrência é idempotente: manter `DataConclusaoUtc` e não criar outra sucessora.
- Alterar frequência/duração atualiza apenas a configuração usada para ocorrências futuras ainda não criadas. Ocorrências existentes e seus prazos permanecem inalterados.
- Excluir uma ocorrência recorrente remove somente aquele item, não reutiliza o número removido e mantém a série programada para os próximos períodos; excluir a série remove todos os seus itens e encerra agendamento e catch-up.
- Para tarefa não recorrente, excluir sem `scope` remove o item normalmente. Para tarefa recorrente, exigir `scope=occurrence` ou `scope=series`.
- A rotina hosted deve processar períodos vencidos e fazer catch-up após reinício, criando cada período vencido que ainda caiba no total configurado e sem duplicar ocorrência graças à unicidade do par série/número.
- Login válido emite JWT Bearer. Login inválido retorna `401 ProblemDetails` sem revelar se o username existe.
- CRUD de contas de usuário não faz parte desta funcionalidade. O escopo de CRUD é somente tarefas.
- Persistir dados em SQLite local usando EF Core migrations. O caminho do arquivo deve ser configurável e ter um valor padrão local da aplicação.
- Paginação ordena de forma estável por data de criação UTC e, como desempate, por ID. Página além do fim retorna coleção vazia, não erro.

## Critérios de aceite verificáveis

1. Cadastro cria usuários com username e senha sem e-mail; usernames case-insensitive duplicados recebem `409 ProblemDetails`.
2. Senhas são persistidas somente como hash seguro.
3. Login válido retorna JWT Bearer e login inválido retorna `401 ProblemDetails`.
4. Usuário autenticado consegue criar, listar, consultar, atualizar e excluir as próprias tarefas.
5. Usuário não consegue obter ou modificar tarefa de outro usuário, mesmo conhecendo seu ID; as operações por ID respondem `404` também quando o registro pertence a outra conta.
6. Tarefa criada armazena ID, descrição, instante UTC de criação e `concluido=false` por padrão; `DataConclusaoUtc` começa nula.
7. Descrição obrigatória, não branca, com até 200 caracteres é aceita; vazia/branca ou com 201 caracteres retorna `400 ProblemDetails`.
8. A listagem é exclusiva do usuário autenticado. `page` padrão 1 e `pageSize` padrão 15; páginas contêm no máximo 15 registros. Valores inválidos ou `pageSize` acima de 15 retornam `400`; página além do fim retorna lista vazia.
9. A ordenação por data de criação UTC e ID é estável para dados inalterados, permitindo percorrer páginas sem repetição ou omissão.
10. Dados permanecem no SQLite local após reiniciar a API; migrations do EF Core criam e atualizam o schema.
11. Erros de validação e demais erros cobertos pelo contrato usam `ProblemDetails`; exclusão bem-sucedida retorna `204`.
12. Criar ou marcar uma tarefa como concluída define `DataConclusaoUtc` em UTC pelo servidor; mantê-la concluída preserva a data existente e desmarcá-la limpa a data para `null`. O cliente não consegue atribuir uma data arbitrária de conclusão.
13. A API aceita as frequências `Diaria`, `Semanal` e `Mensal`, persiste/devolve a frequência e a duração como quantidade de períodos na unidade correspondente, e exige a duração inteira dentro dos limites confirmados quando há frequência.
14. Frequência inválida, duração ausente, abaixo de 1 ou acima do limite por frequência, e duração enviada sem frequência retornam `400 ProblemDetails`.
15. `DuracaoRecorrencia` limita a série ao total configurado de ocorrências, incluindo a inicial; a primeira tem número `1`.
16. A primeira ocorrência recorrente tem `DataPrevistaUtc` igual ao instante UTC de criação, definido pelo servidor; o cliente não consegue definir ou alterar prazo.
17. Cada prazo seguinte usa o prazo anterior como âncora e soma um dia, sete dias ou um mês conforme a frequência. Para mês sem dia equivalente, o prazo usa o último dia do mês seguinte com o mesmo horário UTC.
18. Concluir antes do prazo preserva a ocorrência concluída e cria imediatamente a próxima com o prazo calculado. Vencer sem concluir preserva a ocorrência pendente e cria a próxima ao virar o período.
19. Catch-up após reinício cria cada período vencido que ainda caiba na duração total; nenhuma série excede seu limite.
20. `(SerieRecorrenciaId, NumeroOcorrencia)` é único, impedindo duplicatas durante execução normal ou catch-up.
21. Criar uma ocorrência recorrente já concluída cria imediatamente a sucessora se ainda houver limite, mantendo a inicial concluída.
22. Desmarcar uma ocorrência antiga depois da criação de sua sucessora limpa somente a ocorrência antiga e preserva a sucessora.
23. Repetir a conclusão da mesma ocorrência mantém a data já gravada e não cria sucessora duplicada.
24. Alterar frequência/duração atualiza a série para itens futuros sem alterar ocorrências existentes ou seus prazos.
25. Excluir TODO não recorrente sem `scope` funciona; para ocorrência recorrente, `scope` ausente ou inválido retorna `400 ProblemDetails`.
26. `DELETE ?scope=occurrence` remove somente a ocorrência, não reutiliza seu número e deixa a série gerar ocorrências futuras.
27. `DELETE ?scope=series` remove todas as ocorrências e impede geração e catch-up futuros.

### Critérios propostos de persistência para revisão de Geto

1. `RecurrenceSeries` mantém frequência, duração, estado ativo, próximo número e próximo prazo como fonte de verdade; cada ocorrência referencia essa série e mantém seu próprio número e prazo.
2. `(SerieRecorrenciaId, NumeroOcorrencia)` é protegido por índice único no banco. Excluir fisicamente uma ocorrência não recua o cursor nem permite reutilizar seu número.
3. `scope=series` exclui em cascade a série e suas ocorrências.
4. Alterar frequência/duração mantém itens e prazos existentes; a nova configuração vale para gerações futuras. Reduzir a duração abaixo do maior número já gerado retorna `400`; definir duração igual ao maior número já gerado termina a série sem novas ocorrências.
5. Converter tarefa avulsa em recorrente vincula o item existente como ocorrência `1`, com vencimento no instante UTC da conversão e cursor pronto para o número seguinte.
6. Definir frequência/duração como nulas desativa a série e interrompe novas gerações sem apagar itens existentes.
7. `RecurrenceSeries` guarda `UserId` e descrição base além da configuração/cursor; sua vida útil independe das linhas de ocorrência.
8. Após excluir todas as ocorrências individualmente, a série permanece ativa e pode gerar a próxima ocorrência com o proprietário e descrição base corretos. Respostas de ocorrência incluem `SerieRecorrenciaId`; item avulso retorna `null`.
9. `DELETE /api/todo-series/{seriesId}` cancela uma série sem ocorrências; também apaga em cascade quando ainda existem ocorrências e não permite geração/catch-up posterior.
10. Alterar a descrição de uma ocorrência recorrente atualiza a descrição base apenas para novas ocorrências; itens já existentes ficam inalterados.

## Decisões confirmadas

- Incluir cadastro por username e senha, sem e-mail.
- Usar JWT Bearer para autenticação.
- Tratar usernames como únicos sem distinção de maiúsculas/minúsculas.
- Exigir descrição não branca, com limite máximo de 200 caracteres.
- Definir data de criação em UTC no servidor; o campo não é editável pelo cliente. Tarefa começa como não concluída (`false`).
- Definir `DataConclusaoUtc` nullable em UTC pelo servidor, atribuí-la ao criar uma tarefa concluída ou ao marcá-la concluída, preservá-la enquanto continuar concluída e limpá-la ao desmarcar. O cliente não pode definir esse valor diretamente.
- Usar recorrência opcional com frequências `Diaria`, `Semanal` ou `Mensal`; duração é o número total de ocorrências, incluindo a inicial. Primeira ocorrência vence na criação UTC; próximas datas são ancoradas no vencimento anterior e avançam um dia, sete dias ou um mês, usando o último dia do mês quando necessário. Concluir antes do prazo ou criar já concluída gera imediatamente a ocorrência seguinte (se houver limite); vencer pendente gera a próxima na virada. Desmarcar ocorrência antiga não cancela sucessora; concluir novamente é idempotente. Alterar frequência/duração afeta ocorrências futuras, preservando existentes e seus prazos. Exclusão recorrente exige escolher ocorrência ou série; série excluída não tem novos agendamentos. A rotina hosted faz catch-up após reinício. `SerieRecorrenciaId` e `NumeroOcorrencia` asseguram unicidade e a primeira ocorrência é número `1`.
- Usar `page` padrão 1 e `pageSize` padrão 15, com limite máximo 15. Parâmetros inválidos retornam `400`; página além do fim retorna coleção vazia.
- Ordenar por data de criação UTC e depois por ID.
- Usar SQLite local com EF Core migrations, caminho configurável e arquivo padrão local da aplicação.
- Seguir status HTTP convencionais e `ProblemDetails`: login inválido `401`, username duplicado `409`, tarefa ausente ou de outro usuário `404`, validação inválida `400`.
- CRUD é somente para tarefas, não para contas de usuário.

## Fora do escopo confirmado

- E-mail, recuperação de senha, confirmação de conta e funcionalidades de compartilhamento não foram solicitados.
- Não foram especificados filtros, ordenação pelo cliente, anexos ou múltiplas listas nomeadas. Categorias e janelas de agendamento têm requisitos próprios nesta documentação.

## Plano de testes esperado

- Testes de serviço/domínio para unicidade case-insensitive de username, hash de senha, validação de descrição, valores iniciais, transições de conclusão/data UTC e operações CRUD.
- Testar persistência e validação de `FrequenciaRecorrencia` e `DuracaoRecorrencia`: cada frequência/unidade válida, combinação ausente, valor inválido, duração ausente, zero/negativa e duração sem frequência.
- Testes de integração HTTP e SQLite para cadastro/login/JWT, paginação (parâmetros ausentes, inválidos, tamanhos 1 e 15, mais de 15 itens e página além do fim), persistência após reiniciar/recriar o contexto da API e isolamento entre dois usuários.
- Testar acesso cruzado por ID para GET, PUT e DELETE, além da listagem.
- Testar criação concluída e não concluída, transições `false`→`true`→`false`, preservação da data em atualização que continua concluída, criação da data UTC no servidor e que o cliente não consegue atribuir data arbitrária de conclusão.
- Testar limite da série: duração inclui a ocorrência inicial e não gera mais que o total configurado.
- Testar primeira ocorrência com `DataPrevistaUtc` igual a `DataCriacao` UTC e número `1`.
- Testar cálculo de próximos prazos a partir do prazo anterior: diária `+1 dia`, semanal `+7 dias`, mensal `+1 mês`, incluindo clamp de 29/30/31 para último dia do mês menor e preservação do horário UTC.
- Testar que concluir antes do prazo cria imediatamente a ocorrência seguinte com prazo calculado, mantendo a anterior concluída.
- Testar que vencer sem concluir mantém a ocorrência pendente e cria a próxima na virada do período.
- Testar catch-up após reinício: cria cada período vencido que cabe no total restante e não ultrapassa a duração.
- Testar índice/garantia única em `(SerieRecorrenciaId, NumeroOcorrencia)` e idempotência contra duplicatas sob reprocessamento.
- Testar criar uma tarefa recorrente já concluída: manter a ocorrência inicial concluída e criar sua sucessora imediatamente se houver limite restante.
- Testar desmarcar ocorrência antiga após criação da sucessora: limpar somente a ocorrência antiga e manter a sucessora.
- Testar repetir a conclusão da mesma ocorrência: preservar `DataConclusaoUtc` e não criar ocorrência duplicada.
- Testar alteração de frequência/duração da série sem modificar itens existentes ou seus prazos, e aplicar a nova configuração às próximas ocorrências geradas.
- Testar exclusão não recorrente sem `scope`, `400` para scope ausente/inválido em recorrente, `scope=occurrence` mantendo série/numeração e `scope=series` removendo toda a série e impedindo catch-up futuro.
- Testar título obrigatório e limite de 200 caracteres, categoria opcional e ownership check, prioridade válida/default, criação dos quatro defaults por usuário e `409` ao excluir categoria em uso.
- Testar CRUD/isolamento de categorias e propagação de título, categoria, prioridade e proprietário para ocorrências novas, inclusive quando não restarem linhas de ocorrência; alterações não modificam tarefas já criadas.
- Testar `ProblemDetails` e status HTTP para descrição inválida, username duplicado, credenciais inválidas, parâmetro de página inválido e recurso ausente/alheio.

### Testes propostos de persistência para revisão de Geto

- Persistir e recarregar `RecurrenceSeries` como fonte de verdade, verificar vínculo de `TodoItem` recorrente e índice único `(SerieRecorrenciaId, NumeroOcorrencia)`.
- Excluir fisicamente uma ocorrência e verificar que o cursor/número seguinte da série permanece monotônico, o número apagado não é reutilizado e a geração futura continua.
- Excluir individualmente todas as ocorrências e verificar que a série ainda gera a próxima com `UserId` e descrição base corretos, além de expor `SerieRecorrenciaId` em cada resposta.
- Excluir uma série e verificar cascade das ocorrências e ausência de novas tarefas por scheduler/catch-up.
- Com todas as ocorrências já removidas, chamar `DELETE /api/todo-series/{seriesId}` e verificar `204`, cancelamento do scheduler e nenhuma geração após isso; testar `404` para série inexistente ou pertencente a outro usuário.
- Alterar frequência/duração e verificar que a configuração nova vale para geração futura, sem mudança em itens/prazos já persistidos; duração abaixo do maior número gerado retorna `400`, igual ao maior número encerra futuras gerações.
- Converter item avulso em recorrente e verificar vínculo como ocorrência `1`, prazo UTC igual ao instante da conversão, cursor preparado para número `2` e próximo prazo calculado conforme frequência.
- Enviar frequência/duração nulas e verificar série inativa sem exclusão das tarefas que já existem.
- Editar descrição de ocorrência recorrente e verificar atualização da descrição base para futuras gerações, sem modificar ocorrências já criadas.

## Nova funcionalidade: janela de agendamento e fuso horário

### Comportamento implementado

Janelas UTC e preferência IANA já estão implementadas. O agendamento por janela é distinto de `DataPrevistaUtc`, que permanece o prazo/cursor da recorrência. As rotas implementadas são `POST/PUT /api/todos` para informar a janela e `GET/PUT /api/preferences` para consultar/atualizar o fuso autenticado.

### Contrato implementado para TODO

Requests de criação/edição aceitam `DataInicioUtc` e `DataFimUtc` como `DateTimeOffset?`; respostas TODO incluem ambos como datas UTC nullable. O par deve estar ausente/nulo para tarefa sem intervalo e deve ser fornecido junto; não aceitar apenas um limite. Respostas incluem ambos como `null` para tarefa sem agendamento.

No request, aceitar timestamps ISO 8601 com offset (`Z` ou `±HH:mm`) e normalizar ambos para UTC antes de persistir. A resposta retorna timestamps normalizados em UTC (`Z`). O fuso IANA da conta determina interpretação/exibição civil e comportamento recorrente; os instantes persistidos permanecem UTC. Além de fim posterior ao início, ambos os instantes, convertidos para o fuso do usuário, devem cair na mesma data local; caso contrário, retornar `400 ValidationProblemDetails`.

Exemplo de extensão do corpo existente (os outros campos do contrato TODO continuam necessários):

```json
{
  "dataInicioUtc": "2026-11-09T09:00:00-03:00",
  "dataFimUtc": "2026-11-09T10:00:00-03:00"
}
```

Após normalização, ambos equivalem a `2026-11-09T12:00:00Z`. Os nomes `DataInicioUtc` e `DataFimUtc` descrevem o formato persistido/resposta; DTOs JSON seguem camelCase.

### Preferência de fuso horário implementada

Associar a cada conta uma preferência `TimeZoneId` IANA, com padrão `America/Fortaleza`. O identificador é validado pelo backend; não inferir fuso do servidor nem do navegador. A preferência é particular ao usuário autenticado. Migration SQLite aditiva adiciona coluna `TimeZoneId` não nula em usuários existentes e novos, preenchendo linhas existentes com `America/Fortaleza` sem alterar os demais dados.

Contrato implementado: `GET /api/preferences` retorna `{ "timeZoneId": "America/Fortaleza" }`; `PUT /api/preferences` recebe `{ "timeZoneId": "<IANA>" }` e retorna a preferência persistida (`200`). Ambos exigem Bearer. O identificador é validado com as zonas disponíveis ao runtime; inválido retorna `400 ValidationProblemDetails`. Alterar o fuso afeta apenas janelas de ocorrências futuras ainda não geradas; datas UTC já persistidas permanecem inalteradas. Antes de salvar, a agenda futura é projetada com o novo fuso; colisão retorna `409 ProblemDetails` e mantém a preferência anterior.

### Validação, sobreposição e interação com recorrência

- Rejeitar intervalo parcial, timestamps inválidos, `DataFimUtc <= DataInicioUtc` e início/fim que, no fuso do usuário, caiam em datas locais diferentes com `400 ValidationProblemDetails`.
- Validar conflitos no horário local do usuário autenticado, no fuso IANA configurado, com janelas semiabertas `[início, fim)`. Há conflito quando `a.inicio < b.fim && b.inicio < a.fim`; igualdade de fronteira é permitida. Intervalo parcial, inválido ou com fim menor/igual ao início retorna `400 ValidationProblemDetails`. Um conflito bloqueia criação, edição ou preferência com `409 ProblemDetails`; a operação não deve persistir parcialmente.
- A checagem inclui todas as janelas completas de TODOs persistidos do usuário (concluídas ou pendentes) e projeta todos os números não gerados de cada `RecurrenceSeries` ativa com janela-base completa, de `NextOccurrenceNumber` até `Duration`, inclusive. Recorrências sem janela-base (ambos os campos ausentes) seguem sua recorrência normal, mas não têm slots de agenda projetados. A série com janela persiste `ScheduleAnchorLocalDate`, `ScheduleAnchorOccurrenceNumber`, `ScheduleStartLocalTime` e `ScheduleEndLocalTime`, além da frequência/duração; isso mantém projeção possível mesmo depois que todas as ocorrências foram excluídas. Criar/converter para recorrente ancora na ocorrência número 1. Ao editar uma ocorrência número N com janela, a data/horas locais informadas viram nova âncora e `ScheduleAnchorOccurrenceNumber=N`; o próximo número futuro k é projetado avançando `k-N` períodos a partir dessa âncora. Assim, editar a ocorrência 2 projeta a 3 após um período, sem pular intervalo. Para cada número futuro, avançar a data civil pela frequência, resolver início/fim com `TimeZoneId` vigente e regras DST, e converter a janela para UTC. Ocupar slots persistidos primeiro; não inserir na comparação o slot projetado correspondente ao mesmo (SerieRecorrenciaId, NumeroOcorrencia). Séries inativas e tarefas sem janela não geram slots projetados. Excluir/encerrar série remove projeções; excluir uma ocorrência remove somente seu slot persistido, mantendo slots futuros. A verificação é por usuário dono, nunca global.
- Recorrência de janelas segue a frequência de calendário no fuso IANA da conta: diária avança um dia civil, semanal sete dias civis e mensal um mês civil com clamp para o último dia. Preservar os horários civis locais de início e fim da âncora em cada slot. Uma mudança de fuso usa os mesmos valores-base locais com o novo identificador para slots ainda não gerados; não recalcula nem modifica janelas já persistidas. Depois de formar o horário civil de cada ocorrência, convertê-lo para UTC; a regra já aprovada para `DataPrevistaUtc` continua independente: prazo e janela são campos distintos.
- Preservar separadamente os dois horários de parede da janela: hora local de início e hora local de fim no fuso IANA vigente. Não preservar a duração absoluta em UTC; a diferença entre os instantes UTC de início e fim pode variar ao cruzar uma transição DST. Em transição DST, horário local inexistente avança pelo tamanho do gap (por exemplo, 02:30 durante gap de uma hora torna-se 03:30); horário ambíguo usa a primeira ocorrência cronológica daquele horário local. Estas regras aplicam-se à entrada local e às janelas futuras projetadas. Mudanças de fuso afetam somente slots ainda não gerados e devem ser rejeitadas com `409 ProblemDetails` sem atualizar preferência se causarem conflito.
- Tarefas de uma série geradas depois de editar título/categoria/prioridade continuam seguindo regras já aprovadas. Edição dos campos da janela atualiza os valores base apenas para janelas futuras ainda não geradas; janelas já persistidas permanecem intactas. Catch-up usa a mesma projeção e não cria slots conflitantes. Se o worker encontrar conflito inesperado ao gerar uma ocorrência, não persiste essa ocorrência, desativa a série e registra log de erro com série, número e janela conflitante. O worker não produz resposta HTTP. Edição posterior da configuração pode reativar a série somente após validar que os slots restantes não conflitam e que a duração continua dentro dos limites.

### Limites e comportamento de conflito

Limites confirmados para duração total da recorrência, incluindo a ocorrência inicial: diária até `3650`, semanal até `520`, mensal até `120`. Valores acima do limite ou menores que `1` retornam `400 ValidationProblemDetails`. A projeção de sobreposição percorre as ocorrências até esse total; não há limite adicional de horizonte nem materialização antecipada exigida só para validar colisão.

O contrato de conflito é `409 ProblemDetails`; respostas devem identificar de forma útil o(s) intervalo(s)/TODO conflitante(s), sem expor dados de outra conta. Para atualização, excluir da comparação o próprio TODO e o slot persistido/projetado correspondente; comparar slots distintos da mesma série entre si e com todos os slots do usuário.

### Migration SQLite e preservação dos dados

Migrations já implementadas: `20261107170415_AddSchedulingWindowsAndTimeZones` adiciona janelas UTC nullable em TODO, `TimeZoneId` não nulo com default/backfill `America/Fortaleza`, e data-âncora local/horários locais de início/fim nullable em série; `20261107171051_AddScheduleAnchorOccurrenceNumber` adiciona `ScheduleAnchorOccurrenceNumber` nullable à série. São migrations SQLite aditivas, sem recriar tabelas nem apagar/reescrever dados. Séries preexistentes e séries sem janela mantêm campos de âncora nulos e não projetam slots; não são inferidas janelas para dados existentes. Os demais valores de tarefas, usuários, categorias, ocorrências e cursores são preservados.

### Critérios de aceite e testes para revisão

1. Criar/editar TODO aceita ambos os limites ou ambos nulos; valor parcial, formato/offset inválido, fim não posterior ao início ou início/fim em datas locais diferentes no fuso da conta retorna `400 ValidationProblemDetails`.
2. Converter `-03:00` e `Z` para o mesmo instante UTC e responder ambos os limites em UTC; fuso local não altera o instante persistido.
3. Consulta e paginação incluem os dois campos nullable; tarefa sem janela retorna ambos `null`.
4. Preferência `GET/PUT /api/preferences` exige autenticação, aceita IANA válido, persiste por usuário e retorna padrão `America/Fortaleza` para conta sem preferência; testar isolamento.
5. Testar limites semiabertos, adjacência permitida, contenção, conflitos com TODO persistido e exclusão de si mesmo na edição; usuário diferente não gera conflito.
6. Criar conflitos projetados entre: duas séries ativas com janela-base completa; ocorrência persistida e ocorrência projetada de outra série; série com janela e TODO avulso; e dois slots da mesma série. Verificar que slot já persistido não é contado duas vezes; recorrência sem janela-base e série inativa/cancelada não geram projeções.
7. Validar limites de frequência (diária `1..3650`, semanal `1..520`, mensal `1..120`), contando ocorrência inicial, e rejeitar valor acima do máximo com `400`.
8. Testes DST cobrem horário inexistente avançando pelo tamanho do gap e horário ambíguo escolhendo primeira ocorrência cronológica; testar janelas através da transição e UTC retornado. Testar edição da ocorrência #2 criando âncora #2 e projetando #3 após exatamente um período; criação/conversão ancora na ocorrência #1.
9. Mudar fuso sem conflito altera a preferência e recalcula só slots futuros não gerados; mudança que introduza colisão retorna `409`, preserva preferência anterior e não modifica ocorrências existentes.
10. Colisão em request de criação/edição/mudança de fuso retorna `409 ProblemDetails` sem gravação parcial; resposta não revela informação de outros usuários. Colisão inesperada no worker não produz HTTP: não persiste ocorrência, desativa a série e registra log de erro; edição válida sem colisão pode reativá-la.
11. Aplicar ambas as migrations aditivas em SQLite com dados existentes e verificar preservação integral de usuários/tarefas/séries, default `America/Fortaleza` em todos os usuários existentes, campos de janela/âncora/número de âncora nulos e recorrências existentes sem slots projetados.
