# Integração Angular com a TodoAPI

Guia do contrato HTTP implementado pela API ASP.NET Core para o cliente Angular. O front atual já usa os campos de título, categoria e prioridade e oferece CRUD de categorias; esta integração alinha esses modelos e serviços aos campos de janela `dataInicioUtc`/`dataFimUtc` e à preferência de fuso IANA em `/api/preferences`.

## Execução e URL

- API local HTTP: `http://localhost:5091` (perfil `http` do projeto ASP.NET Core). Inicie-a com `dotnet run --launch-profile http` dentro de `TodoAPI/`; o perfil `https` usa `UseHttpsRedirection()` e pode redirecionar as chamadas do proxy para `https://localhost:7054`.
- API local HTTPS: `https://localhost:7054` (o perfil HTTPS também disponibiliza HTTP em `http://localhost:5091`).
- Angular: `npm start` dentro de `Front-TodoApp/` inicia o servidor de desenvolvimento. O proxy configurado em `Front-TodoApp/proxy.conf.json` encaminha `/api/**` para `http://localhost:5091`.
- No Angular, use caminhos relativos, por exemplo `/api/auth/login`. Para chamadas diretas à API, prefixe a rota com `http://localhost:5091`.
- Em ambiente `Development`, a documentação interativa fica em `http://localhost:5091/swagger` e o documento OpenAPI em `/openapi/v1.json`.

Os corpos JSON usam nomes camelCase. Os enums são serializados como strings (`"Media"`, `"Diaria"`), não como índices numéricos.

## Cadastro, login e sessão

Não há e-mail no contrato. `username` é único sem diferenciar maiúsculas de minúsculas; a API remove espaços das pontas e valida o tamanho máximo de 50 caracteres. A senha é obrigatória e não pode ser branca.

### Cadastro

`POST /api/auth/register`

```json
{
  "username": "ana",
  "password": "senha-escolhida"
}
```

Resposta `201 Created`:

```json
{
  "id": 7,
  "username": "ana"
}
```

O cadastro cria também as categorias iniciais pertencentes à conta: `Estudos`, `Reunião`, `Trabalho` e `Pessoal`.

### Login

`POST /api/auth/login`, com o mesmo formato de entrada. Em sucesso (`200 OK`):

```json
{
  "token": "<jwt>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-11-07T18:30:00Z"
}
```

Envie o token em todas as rotas protegidas:

```http
Authorization: Bearer <jwt>
```

O interceptor atual do Angular já anexa o token de sessão salvo em `sessionStorage`. O login inválido retorna `401` com `ProblemDetails`; ao receber `401`, a sessão deve ser invalidada e o usuário direcionado ao login. Nome de usuário duplicado retorna `409` com `ProblemDetails`. Erros de validação do corpo retornam `400`.

## Tarefas TODO

Todas as rotas `/api/todos` exigem Bearer token. Cada resposta e consulta é limitada ao usuário autenticado. Um ID inexistente ou pertencente a outra conta retorna `404`, sem revelar a existência de dados alheios.

### Formato de TODO

```json
{
  "id": 31,
  "titulo": "Preparar apresentação",
  "descricao": "Revisar os slides",
  "categoriaId": 2,
  "categoriaNome": "Trabalho",
  "prioridade": "Media",
  "dataCriacao": "2026-11-07T15:00:00Z",
  "concluido": false,
  "dataConclusaoUtc": null,
  "dataPrevistaUtc": null,
  "frequenciaRecorrencia": null,
  "duracaoRecorrencia": null,
  "serieRecorrenciaId": null,
  "numeroOcorrencia": null
}
```

`titulo` e `descricao` são obrigatórios, não podem conter apenas espaços e aceitam até 200 caracteres. `categoriaId` é opcional e deve identificar uma categoria do mesmo usuário; `categoriaNome` é preenchido na resposta e não é enviado no corpo. `prioridade` aceita `"Baixa"`, `"Media"` ou `"Alta"` e assume `"Media"` quando omitida ou nula.

`dataCriacao`, `dataConclusaoUtc` e `dataPrevistaUtc` são instantes UTC enviados em formato ISO 8601. O cliente não define timestamps: a criação e conclusão são marcadas no servidor; `dataConclusaoUtc` é nula enquanto pendente e limpa ao desmarcar. Para tarefas sem recorrência, `dataPrevistaUtc`, `frequenciaRecorrencia`, `duracaoRecorrencia`, `serieRecorrenciaId` e `numeroOcorrencia` são nulos. Em recorrências, a frequência é `"Diaria"`, `"Semanal"` ou `"Mensal"`, e a duração conta ocorrências incluindo a inicial: de 1 a 3650 diária, 1 a 520 semanal ou 1 a 120 mensal.

### Criar e editar

`POST /api/todos` cria uma tarefa (`201 Created`). Exemplo sem recorrência:

```json
{
  "titulo": "Preparar apresentação",
  "descricao": "Revisar os slides",
  "concluido": false,
  "categoriaId": 2,
  "prioridade": "Alta",
  "frequenciaRecorrencia": null,
  "duracaoRecorrencia": null
}
```

`PUT /api/todos/{id}` substitui os campos editáveis e recebe o mesmo formato. Envie `titulo`, `descricao`, `concluido`, `categoriaId`, `prioridade`, `frequenciaRecorrencia` e `duracaoRecorrencia`; use `null` para categoria ou recorrência ausente. Frequência e duração devem ser informadas juntas; duração deve respeitar o limite da frequência (diária 1–3650, semanal 1–520, mensal 1–120). `DataConclusaoUtc`, `DataPrevistaUtc`, IDs e número de ocorrência são somente leitura.

Uma recorrência começa com ocorrência número `1`, prevista no instante UTC de criação/conversão. A próxima data se ancora na anterior: mais um dia, sete dias ou um mês; no caso mensal, dia inexistente é ajustado para o último dia do mês. Concluir uma ocorrência cria a próxima imediatamente, se houver ocorrências restantes; a tarefa inicial enviada já concluída também gera sua sucessora. O serviço hosted cria ocorrências vencidas e faz catch-up após reinício. Desmarcar uma ocorrência antiga não remove a sucessora e repetir a conclusão não duplica a ocorrência.

Alterar título, descrição, categoria, prioridade, frequência ou duração na edição de uma ocorrência altera também os valores base/configuração da série para ocorrências que ainda serão geradas; itens e prazos já existentes permanecem. A duração não pode ser reduzida abaixo do maior número já gerado. Os campos `serieRecorrenciaId` e `numeroOcorrencia` identificam a série e a posição de cada ocorrência.

### Listar e consultar

- `GET /api/todos?page=1&pageSize=15` retorna somente tarefas do usuário atual, ordenadas por `dataCriacao` crescente e depois `id` crescente.
- `page` começa em 1 (padrão 1). `pageSize` aceita de 1 a 15 (padrão 15).
- Resposta `200 OK`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 15,
  "totalCount": 0
}
```

Uma página após o fim retorna `items: []`. Página menor que 1, tamanho menor que 1 ou maior que 15 retorna `400` com erros de validação.
- `GET /api/todos/{id}` retorna `200 OK` com um TODO, ou `404` com `ProblemDetails` para ID ausente ou alheio.

### Excluir TODO ou recorrência

`DELETE /api/todos/{id}` exclui uma tarefa sem recorrência (`204 No Content`). Para uma ocorrência recorrente, a API exige um dos escopos válidos:

- `DELETE /api/todos/{id}?scope=occurrence` remove somente aquela ocorrência; a série e seu cursor permanecem e não reutilizam o número removido.
- `DELETE /api/todos/{id}?scope=series` remove a série e todas as ocorrências.
- `DELETE /api/todo-series/{serieRecorrenciaId}` também encerra a série inteira, inclusive quando não há mais ocorrências para selecionar.

Na interface atual, o botão Excluir chama a API imediatamente, sem modal/confirmação. Para uma ocorrência recorrente, enviar `scope=occurrence`, removendo somente o item selecionado e mantendo a série ativa; não usar `scope=series` nessa ação. O contrato da API também aceita `scope=series` para outros fluxos que explicitamente encerrem uma série, mas a lista não oferece essa opção. Para tarefas avulsas, não enviar `scope`; fornecer escopo resulta em `400`. Sucesso retorna `204`; recurso ausente ou de outro usuário retorna `404`.

## Categorias

Todas as rotas `/api/categories` exigem Bearer token e operam apenas nas categorias do usuário logado. O cadastro cria quatro categorias iniciais listadas acima. O corpo de criação/edição é `{ "nome": "Leitura" }`; o nome é obrigatório e não pode ser branco. A resposta é `{ "id": 5, "nome": "Leitura" }`.

| Operação | Rota | Resposta de sucesso |
|---|---|---|
| Listar categorias próprias | `GET /api/categories` | `200 OK`, array de categorias |
| Consultar categoria própria | `GET /api/categories/{id}` | `200 OK`, categoria |
| Criar | `POST /api/categories` | `201 Created`, categoria |
| Renomear | `PUT /api/categories/{id}` | `200 OK`, categoria |
| Excluir | `DELETE /api/categories/{id}` | `204 No Content` |

Categoria ausente ou pertencente a outro usuário resulta em `404`. Categoria usada por qualquer tarefa ou como categoria base de uma série não pode ser excluída: a API retorna `409 Conflict` com `ProblemDetails` (`title: "Categoria em uso."`). A tela deve manter a categoria selecionável enquanto usada e apresentar o erro retornado se a exclusão falhar.

## Erros e respostas HTTP

O corpo de erro depende da rota e do tipo de falha. Validações de entrada usam `ValidationProblemDetails` (`400`) e incluem erros de campo quando disponíveis. Credenciais inválidas em `POST /api/auth/login`, username duplicado e conflitos ao excluir categoria em uso retornam `ProblemDetails`. Ausência/invalidade do Bearer token pode retornar `401` sem corpo (challenge JWT). `GET`, `PUT` e `DELETE /api/categories/{id}` retornam `404` sem corpo quando a categoria não existe ou não pertence ao usuário. Em outras rotas documentadas, os `404` customizados retornam `ProblemDetails`. O cliente deve tratar o status HTTP como fonte principal e só ler `status`, `title`, `detail` ou `errors` quando houver corpo; não assuma que toda resposta de erro contém JSON.

| Status | Uso principal |
|---|---|
| `200` | Consulta, login ou atualização concluída |
| `201` | Cadastro, TODO ou categoria criada |
| `204` | Exclusão concluída, sem corpo |
| `400` | JSON/campos inválidos, janela parcial/invertida ou em datas locais diferentes, fuso IANA inválido, paginação ou recorrência inválida |
| `401` | Login com credenciais inválidas (`ProblemDetails`); token ausente/inválido/expirado em rota protegida (corpo possivelmente vazio) |
| `404` | TODO/série inexistente ou alheia (`ProblemDetails`); categoria ausente/alheia em `GET`/`PUT`/`DELETE /api/categories/{id}` (sem corpo) |
| `409` | Username já cadastrado, categoria em uso ou conflito de agenda em criação/edição/mudança de fuso (`ProblemDetails`) |

## Fluxo de telas sugerido

1. A tela de cadastro envia username e senha para `/api/auth/register`; depois do cadastro, a pessoa entra pela tela de login.
2. A tela de login armazena o JWT retornado e navega para a lista. O interceptor envia o Bearer token nas chamadas seguintes.
3. A tela de tarefas carrega `/api/categories` para as opções de categoria e `/api/todos` para a página atual. Paginação deve respeitar `pageSize <= 15` e usar `totalCount` para navegação.
4. Formulário de criação/edição envia os campos mutáveis definidos acima. Na edição recorrente, deixe claro que ajustes de configuração se aplicam às ocorrências futuras.
5. Para excluir pela lista, faça a chamada imediatamente: tarefa recorrente usa `scope=occurrence` e tarefa avulsa usa a rota sem query string. A ação exclui somente a ocorrência selecionada e não encerra a série. A rota `/api/todo-series/{id}` permanece no contrato para outros fluxos que precisem encerrar uma série sem ocorrência existente.
6. A tela de categorias lista/cria/renomeia categorias e trata `409` ao excluir uma categoria em uso.

Cada usuário vê somente suas tarefas e categorias; nunca use dados locais como garantia de acesso. O isolamento é aplicado pelo servidor a partir do JWT e deve ser preservado também ao lidar com `404`.

## Janela de agendamento e fuso horário implementados

O backend implementa os campos de janela nos DTOs e a preferência de fuso nas rotas descritas abaixo. O cliente Angular atual ainda não inclui esses campos nos seus modelos/requests nem tem serviço de preferências; integre-os aos serviços existentes. `dataPrevistaUtc` continua sendo o prazo da recorrência e é distinto da janela de agendamento.

### TODO com janela

`POST /api/todos` e `PUT /api/todos/{id}` aceitam os campos abaixo, incluídos em todas as respostas TODO:

```json
{
  "dataInicioUtc": "2026-11-09T09:00:00-03:00",
  "dataFimUtc": "2026-11-09T10:00:00-03:00"
}
```

Os requests usam `DateTimeOffset?`; os campos são opcionais somente como par: envie ambos com timestamps ISO 8601 contendo `Z` ou offset, ou ambos `null` para tarefa sem janela. O backend normaliza para UTC; resposta DTO inclui instantes ISO 8601 em UTC (`Z`). Exemplo: os valores acima representam `2026-11-09T12:00:00Z`. São instantes, não strings locais sem offset. O frontend deve manter o instante retornado e formatá-lo para exibição usando a preferência IANA da conta.

O fim deve ser posterior ao início. Ao converter ambos para o fuso do usuário, os limites também devem cair na mesma data local; janela parcial, invertida/igual ou que atravesse a meia-noite local retorna `400 ValidationProblemDetails`.

`DataInicioUtc`/`DataFimUtc` não substituem `DataPrevistaUtc`, nem alteram `DataConclusaoUtc`. Para recorrências, cada ocorrência terá janela UTC própria calculada conforme regras de calendário no fuso configurado; datas de início/fim não devem ser calculadas pelo navegador sem consultar a preferência e as regras do backend.

### Preferência de fuso horário

A preferência autenticada por usuário usa `GET /api/preferences` (`200`) e `PUT /api/preferences` (`200`), com payload/resposta `{ "timeZoneId": "America/Fortaleza" }`. O padrão é `America/Fortaleza`. O fuso deve ser identificador IANA reconhecido pelo runtime; valor inválido retorna `400 ValidationProblemDetails`. O Angular deve carregar a preferência antes de interpretar/agendar horas civis. Alterar o fuso afeta somente janelas futuras ainda não geradas; instantes UTC já persistidos permanecem iguais. Uma janela UTC existente pode, portanto, passar a aparecer em outra data local após a mudança. Se o novo fuso criar conflito projetado, a API retorna `409 ProblemDetails` e conserva a preferência anterior. Ambas as rotas exigem Bearer; challenge por token ausente/inválido pode retornar `401` sem corpo.

### Validação, conflito e DST

O contrato rejeita (`400 ValidationProblemDetails`) apenas um dos limites, timestamp inválido, fim menor/igual ao início ou limites que, convertidos ao fuso do usuário, caiam em datas locais diferentes. Sobreposição bloqueia a operação com `409 ProblemDetails`. A comparação ocorre no calendário/fuso local do usuário e usa intervalos semiabertos `[início, fim)`, portanto uma janela que termina exatamente quando outra começa é permitida.

A validação compara todas as janelas completas persistidas do usuário (concluídas ou pendentes) com slots projetados somente de séries ativas com janela-base completa. Recorrências sem janela-base (ambos os campos ausentes) seguem a recorrência normal sem slots de agenda projetados. A série persiste data local da âncora, número da ocorrência que a define e horários locais de início/fim. Ao criar/converter uma recorrência, a âncora é a ocorrência número 1; ao editar a ocorrência número N com janela, a janela informada define nova âncora N, e cada ocorrência futura k avança por k−N períodos. Assim, ao editar a ocorrência #2, a #3 é projetada um período depois da nova âncora. Para cada série elegível, projetar cada número ainda não gerado desde `nextOccurrenceNumber` até `duracaoRecorrencia`, inclusive, usando essa âncora, frequência e fuso IANA atual. Para cada slot, avançar a data civil em um dia, sete dias ou um mês (clamp ao último dia), resolver DST e converter a janela para UTC. As séries mantêm essa âncora mesmo sem TODOs persistidos, pois exclusões individuais não cancelam a agenda. O slot projetado da mesma série+número que já está persistido não deve ser contado duas vezes. Série cancelada/inativa não projeta slots; excluir ocorrência remove só sua janela persistida e mantém projeções futuras. Ao editar, exclua o próprio TODO/slot da comparação, mas compare os outros slots da própria série.

Requests de criar/editar TODO ou mudar preferência de fuso com colisão retornam `409 ProblemDetails` e não persistem a alteração. O worker de recorrência não responde HTTP: se encontrar conflito inesperado ao gerar janela, não persiste a ocorrência, desativa a série e registra log de erro. Edição posterior pode reativá-la somente depois que a configuração passar pela validação de conflito e respeitar os limites.

Na mudança de horário de verão, preservar os horários de parede de início e fim separadamente no fuso IANA vigente; não preservar a duração absoluta em UTC, que pode variar após conversão. Um horário local inexistente avança pelo tamanho do gap (por exemplo, 02:30 em um gap de uma hora passa a 03:30); um horário ambíguo usa a primeira ocorrência cronológica. A geração usa calendário local do fuso IANA para recorrência diária/semanal/mensal e converte cada limite para UTC.

### Limites e migration

Limites da duração total de recorrência incluem a ocorrência inicial: diária de 1 a 3650, semanal de 1 a 520 e mensal de 1 a 120. O Angular deve apresentar erro de validação se o valor ultrapassar o limite; não há limite adicional de horizonte/materialização antecipada definido para a checagem de conflito. Os limites de duração mínima/máxima de uma janela de TODO não foram especificados; o cliente deve apresentar a validação retornada pela API sem inventar valores.

A migration SQLite aditiva `20261107170415_AddSchedulingWindowsAndTimeZones` adiciona `DataInicioUtc`/`DataFimUtc` nullable em TODO; `TimeZoneId` não nulo em usuário com default/backfill `America/Fortaleza`, inclusive para contas existentes; e âncora de data local/horários de parede locais de início/fim nullable em série. A migration `20261107171051_AddScheduleAnchorOccurrenceNumber` adiciona o número da ocorrência âncora. Séries existentes e séries sem janela permanecem com campos de âncora nulos, não ganham janela inferida e não geram slots projetados. Datas, conclusão, categoria, proprietário, ocorrência e cursor de recorrência já persistidos não são recalculados.

## Alterações necessárias no Angular

Os modelos e serviços Angular já incluem TODOs com título, categoria, prioridade e APIs de categoria, mas ainda não incluem `dataInicioUtc`/`dataFimUtc` nem serviço de `/api/preferences`. Acrescente esses campos e a chamada de preferência aos clientes existentes, preservando os modelos atuais.

### Modelos e serviços

- Atualize os modelos tipados de TODO com os campos da seção “Formato de TODO”. `prioridade` na resposta é sempre um dos enums string `Baixa`, `Media` ou `Alta`; `categoriaId` e `categoriaNome` são anuláveis.
- Inclua `titulo`, `descricao`, `concluido`, `categoriaId`, `prioridade`, `frequenciaRecorrencia` e `duracaoRecorrencia` nos corpos de criação/edição. Não envie `categoriaNome`, timestamps, ID, ID de série ou número de ocorrência.
- Adicione serviço de categorias para `GET/POST/PUT/DELETE /api/categories`, mantendo HTTP nos serviços e estado/apresentação nos componentes. Reuse o interceptor Bearer existente e preserve compatibilidade com SSR.
- O registro cria quatro categorias iniciais. Carregue categorias do usuário autenticado pela API; não use uma lista fixa como fonte de dados. A categoria é opcional nos TODOs.

### Formulários e interface

- O formulário de TODO deve ter título e descrição obrigatórios, cada um limitado a 200 caracteres, seletor de categoria opcional, prioridade (`Baixa`, `Media`, `Alta`, inicial `Media`) e os campos de recorrência existentes. Mostrar erros de validação sem perder os valores digitados.
- A edição deve enviar o corpo mutável completo. Para um TODO recorrente, informar antes de salvar que alterações em título, descrição, categoria, prioridade e configuração de recorrência afetam ocorrências futuras, não as já criadas.
- Disponibilize uma tela ou painel de categorias com listagem, criação, renomeação e exclusão. A escolha exata entre página, painel ou modal pode seguir a estrutura visual existente, mas as quatro operações devem chamar a API e refletir o resultado nela.
- Ao excluir categoria em uso, manter a categoria e informar o conflito `409` retornado pela API. Para categorias ou TODOs ausentes/alheios, tratar o status `404`; não presumir que todas essas respostas têm corpo JSON.
- A listagem de TODOs deve apresentar título, descrição, categoria quando definida e prioridade. Conservar estados de carregamento, vazio, falha de rede, validação e sessão expirada já usados pelo front.
- Os estados iniciais de carregamento, erro e lista vazia são mutuamente coerentes: se a primeira consulta `GET /api/todos` falhar, mostrar o erro e a ação de tentar novamente, sem apresentar texto ou botão de lista vazia. Manter o erro visível enquanto a tentativa ainda não tiver sucesso; só apresentar a lista vazia quando uma resposta `200` confirmar `items: []`. Em recargas após haver dados, uma falha deve preservar os itens já carregados e informar o erro, sem substituir os dados por um estado vazio.
- A ação de tentar novamente não deve limpar/fingir resolver o erro antes da resposta. Se a tentativa falhar novamente, manter o erro e não renderizar o estado vazio; se retornar `200` com itens, mostrar os dados; se retornar `200` sem itens, mostrar então o estado vazio.

### Paginação e critérios de aceite

Use `GET /api/todos` com `pageSize` entre 1 e 15 e `totalCount` para navegação; não solicite mais de 15 itens por página. A resposta é ordenada por criação/ID e não oferece filtros por categoria, prioridade ou busca no servidor. Se houver filtros locais, deixe claro que são aplicados aos TODOs carregados pelo cliente e não envie parâmetros de filtro não documentados.

Critérios verificáveis para a alteração:

1. Modelos e clientes Angular refletem os DTOs camelCase de TODO, categoria, cadastro e login; enumerações são enviadas como strings.
2. Criar, editar, concluir/reabrir e excluir TODO preservam os campos suportados; categoria e prioridade exibidas correspondem à resposta da API.
3. CRUD de categoria chama as rotas documentadas, atualiza as opções do formulário e apresenta `409` ao tentar excluir categoria em uso sem removê-la da interface.
4. Paginação funciona com mais de 15 TODOs e respeita os limites de `page`/`pageSize`.
5. Recorrência oferece escopo explícito ao excluir, e os campos de ocorrências futuras seguem a semântica documentada.
6. Cobertura de testes verifica rotas/métodos/corpos dos serviços de TODO, categoria e preferência, autenticação Bearer, respostas vazias/204 e erros relevantes (`400`, `401`, `404`, `409`), além dos fluxos de formulário e estado da lista. Verificar edição da ocorrência #2 criando âncora #2 e projeção da #3 um período depois; verificar migration aditiva preservando dados e populando timezone default. Na interface, uma falha inicial ou repetida de carregamento não pode mostrar o CTA/texto de lista vazia; testar erro persistente ao repetir e resposta `200` vazia como condição para mostrar o estado vazio. Cobrir `400` de validação mantendo o formulário e seus valores, `401` limpando a sessão, `404` (inclusive respostas sem corpo nas rotas de categoria) e `409` ao excluir categoria em uso preservando-a na interface.
