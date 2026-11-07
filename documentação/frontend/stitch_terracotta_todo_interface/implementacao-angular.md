# Interface Foco em Angular

## Objetivo e referências

Evoluir o `Front-TodoApp/` para uma aplicação Angular responsiva, em português, alinhada às duas referências Stitch (`stitch_terracotta_todo_interface/todo_list_dashboard_web/code.html` e `todo_list_mobile_view/code.html`) e ao sistema de design `stitch_terracotta_todo_interface/tarefas_design_system.md`. O HTML é referência visual e de interação, não um contrato de persistência. O front atual já tem lista, CRUD, categorias, prioridade e integração de janelas/fuso; esta alteração separa login, cadastro, lista e formulário TODO em páginas/rotas distintas.

Manter a paleta terracota, neutros, tipografia Inter, espaçamento, cantos e movimentos definidos pelo design system. A pedido da pessoa usuária, remover temporariamente os ícones Material Symbols/Material Icons da interface, a importação de sua fonte e os estilos exclusivos desses ícones. Não copiar scripts imperativos do HTML: estado e eventos devem pertencer aos componentes Angular.

## Comportamento visual

- Desktop: barra lateral com marca Foco, ação para criar tarefa, acesso a categorias e preferências da conta e área principal de listagem; cabeçalho da lista com título/resumo e controle de criação. Itens exibem título, descrição, categoria, prioridade e janela agendada retornados pela API, distinguem tarefas abertas/concluídas e mostram recorrência quando presente.
- Mobile: cabeçalho compacto, filtros horizontais para estado e categoria (aplicados localmente aos itens carregados), lista em cartões, ação flutuante de criação e navegação inferior responsiva. Incluir TODO e editar TODO são páginas navegáveis, não modais; preferências de fuso ficam acessíveis em painel de conta compatível com mobile.
- Cada card deve exibir sempre a data e a hora de criação, convertidas para o fuso da conta. Se `dataInicioUtc`/`dataFimUtc` existirem, exibir a data e hora completas do início e do fim também no fuso da conta. Exibir o nome da categoria quando `categoriaId` estiver definido; se a resposta não trouxer `categoriaNome`, resolver o nome pelo `categoriaId` contra as categorias carregadas.
- Layout deve funcionar em larguras estreitas sem rolagem horizontal da página; todos os controles têm rótulo textual visível, nome acessível e podem ser usados por teclado. Mostrar estados de carregamento, lista vazia, erro de API e envio em andamento sem apresentar dados de exemplo como dados reais.

### Remoção temporária dos ícones

A pessoa usuária decidiu remover os ícones por enquanto porque continuam visualmente inadequados em todas as páginas. Remover a referência à fonte Material Symbols no `src/index.html`, as regras globais `.material-symbols-outlined` em `src/styles.css`, as regras de ícones e ornamentos em `src/app/app.css` e os elementos/glifos correspondentes nos templates. Não deixar blocos decorativos vazios onde havia glifo de marca.

Preservar todas as ações e navegação. Onde o ícone era o único conteúdo, usar rótulos textuais visíveis, em português: concluir/reabrir tarefa, editar/excluir tarefa ou categoria, abrir/fechar configurações e categorias, sair, criar tarefa e fechar diálogos. A navegação móvel mantém seus nomes de seção; a criação móvel também deve exibir texto em seu controle. Elementos decorativos sem ação, como o sol da apresentação de autenticação, podem ser removidos sem substituição. Nenhum botão pode ficar sem nome visível/acessível após a retirada dos glifos.

Critérios de aceite verificáveis:

1. Os templates de login, cadastro, lista e formulário não contêm glifos, elementos vazios ou classes de Material Symbols; `src/index.html` não carrega a fonte Material Symbols e `src/styles.css`/`src/app/app.css` não mantêm regras exclusivas dessa fonte ou dos antigos blocos de ícone.
2. Toda ação que antes era representada apenas por ícone apresenta texto visível adequado e mantém seu comportamento existente, navegação, estado desabilitado e nome acessível. Controles de concluir/reabrir comunicam explicitamente qual ação será executada.
3. Navegação, criação de tarefa, ações de cartão, preferências, categorias, login/cadastro e diálogos continuam utilizáveis por teclado em desktop e mobile; rótulos não causam overflow horizontal.
4. Testes verificam ausência dos glifos/classes de ícone nos templates relevantes e verificam que as ações continuam presentes com rótulos de texto. Contratos da API e lógica das ações permanecem inalterados.
- O sistema de design menciona tema escuro, mas as referências fornecidas mostram somente a aparência clara. A escolha/controle de tema escuro não está especificada e não é requisito de aceite desta entrega.

## Limites entre mockup e API

O contrato atual de `TodoResponse` contém `id`, `titulo`, `descricao`, `categoriaId`, `categoriaNome`, `prioridade`, `dataInicioUtc`, `dataFimUtc`, `dataCriacao`, `concluido`, `dataConclusaoUtc`, `dataPrevistaUtc`, `frequenciaRecorrencia`, `duracaoRecorrencia`, `serieRecorrenciaId` e `numeroOcorrencia`. `titulo` e `descricao` são obrigatórios, não podem conter apenas espaços e aceitam até 200 caracteres. Categoria é opcional e vem de `/api/categories`; prioridade aceita `Baixa`, `Media` ou `Alta` e assume `Media` por padrão. A janela `dataInicioUtc`/`dataFimUtc` é opcional como par. `dataPrevistaUtc` é prazo de recorrência, somente leitura, e não substitui a janela agendada.

São persistíveis nesta integração: título, descrição, conclusão, categoria, prioridade, janela de agendamento, frequência de recorrência (`Diaria`, `Semanal`, `Mensal`) e duração da recorrência, respeitando validação da API. A API também oferece CRUD de categorias e preferência de fuso horário IANA por usuário. Ela não oferece projetos, tags livres, notificações, busca ou filtros de calendário. `dataPrevistaUtc` é gerada pelo backend para recorrências e não é editável pelo formulário. Não inventar persistência local para recursos sem endpoint. Elementos decorativos do mockup como nomes de projeto e contadores de exemplo devem ser removidos ou identificados como indisponíveis.

Os filtros “Concluídas” e categoria podem ser realizados no cliente sobre tarefas carregadas; deixe claro que não são filtros do servidor. Não há endpoint de busca/filtro, calendário agregado nem metadados para “Hoje”, “Próximas”, urgência ou projeto. A API persiste janelas por TODO, descritas abaixo, mas não oferece uma rota de agenda agregada ou filtragem de calendário. O endpoint de tarefas entrega páginas de no máximo 15 itens (`pageSize` 1–15), ordenadas pela data de criação, com `totalCount`; percorra as páginas ou ofereça paginação/carregamento adicional visível. “Hoje” não pode ser calculado com fidelidade apenas por criação/conclusão, embora janelas agendadas possam ser exibidas localmente. A recorrência deve ser representada usando os campos explícitos da API, sem inferir próximas datas a partir de texto.

## Contrato HTTP observado

Rotas derivadas dos controllers de autenticação, TODO, séries, categorias e preferências em `TodoAPI/Controllers/`. A configuração web padrão do ASP.NET Core converte nomes JSON para camelCase (por exemplo, `titulo`, `dataInicioUtc`, `timeZoneId` e `items`); enums são serializados como strings.

| Operação de interface | Método e rota | Contrato e resultado esperado |
| --- | --- | --- |
| Criar conta | `POST /api/auth/register` | `{ username, password }`; `201` devolve `{ id, username }`; `409` quando o nome já existe. |
| Entrar | `POST /api/auth/login` | `{ username, password }`; `200` devolve `{ token, tokenType, expiresAtUtc }`; `401` para credenciais inválidas. O token vale uma hora. |
| Listar tarefas | `GET /api/todos?page={n}&pageSize={1..15}` | Exige `Authorization: Bearer {token}`; `200` devolve `{ items, page, pageSize, totalCount }`. Página inicial 1. |
| Criar tarefa | `POST /api/todos` | Exige Bearer; corpo `{ titulo, descricao, concluido, categoriaId?, prioridade?, dataInicioUtc?, dataFimUtc?, frequenciaRecorrencia?, duracaoRecorrencia? }`; `201` devolve `TodoResponse`; validações retornam `400`, conflito de janela retorna `409`. |
| Editar/concluir | `PUT /api/todos/{id}` | Exige Bearer; envia os campos editáveis completos (título, descrição, concluído, categoria, prioridade, janela e recorrência); `200` devolve tarefa, `400` para validação, `404` para tarefa ausente/não pertencente ao usuário e `409` para conflito de janela. |
| Excluir | `DELETE /api/todos/{id}` | Exige Bearer; `204` em sucesso, `404` se ausente. Para tarefa recorrente, exige `?scope=occurrence` ou `?scope=series`; para tarefa comum, omitir `scope`. |
| Excluir série | `DELETE /api/todo-series/{seriesId}` | Exige Bearer; `204` em sucesso, `404` se a série não pertencer ao usuário ou não existir. |
| Gerenciar categorias | `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}` | Exige Bearer; criar/renomear envia `{ nome }`; `GET` devolve array ou categoria, `POST` devolve `201`, `PUT` devolve `200`, `DELETE` devolve `204`; categoria em uso retorna `409`. |
| Ler/alterar fuso da conta | `GET/PUT /api/preferences` | Exige Bearer; resposta/corpo `{ timeZoneId }` com ID IANA; padrão `America/Fortaleza`; fuso inválido retorna `400`, conflito de janelas ao alterar retorna `409`. |

Recorrência deve ser enviada com frequência e duração em conjunto; campos nulos removem a configuração ativa da série em edição. Frequência sem duração ou duração sem frequência é inválido. A duração total inclui ocorrência inicial e deve respeitar o limite por frequência: diária `1–3650`, semanal `1–520`, mensal `1–120`; em série existente, não pode ser menor que o número mais alto já gerado. Ao excluir uma ocorrência recorrente pela lista, enviar `scope=occurrence` e remover somente a ocorrência selecionada; manter a série ativa. Erros `400/401/404/409` e falhas de rede devem manter contexto e informar o usuário; em `401`, descartar sessão expirada e pedir novo login.

## Correção de exclusão e consistência da lista

### Comportamento observado e problema reportado

O serviço Angular carrega `GET /api/todos` por páginas de até 15 itens e agrega `items` até atingir `totalCount`. A página mantém o resultado em estado local. A pessoa usuária decidiu remover o diálogo de confirmação e excluir imediatamente ao clicar em Excluir. O contrato da API retorna `204` em sucesso, exige `scope=occurrence` para excluir uma ocorrência recorrente e oferece também rotas para excluir séries.

Decisão explícita de interface: não renderizar modal, `<dialog>` ou outra etapa de confirmação para exclusão de TODO. A ação da lista exclui imediatamente apenas o item selecionado; se ele pertencer a uma série recorrente, enviar `scope=occurrence` e preservar a série e as demais ocorrências. A interface não oferece exclusão da série inteira nesta ação. Após `204`, remover o item da tela e sincronizar a lista com a API. Se a chamada falhar, manter o item e apresentar erro.

Não está confirmado se a falha originalmente relatada vinha do handler do clique, da rota/parâmetros, da resposta da API ou da atualização local; também não está confirmado se o item inesperado é dado antigo, duplicado, de outra página ou dado de resposta da API. A implementação deve diagnosticar esses caminhos sem introduzir tarefas de exemplo, cache local ou novas regras de ordenação.

### Comportamento esperado

- Somente exibir tarefas retornadas pelas páginas da API para a sessão atual. Depois de um carregamento bem-sucedido, substituir o resultado de tela pela coleção completa retornada, sem reter itens de carregamentos anteriores nem acrescentar registros locais que a API não retornou.
- Preservar IDs únicos de tarefas e concatenar as páginas de acordo com `page`, `pageSize` e `totalCount`. A lista não deve duplicar itens por re-renderização nem mostrar um item removido ao receber uma resposta posterior sem esse item.
- O clique em Excluir inicia imediatamente `DELETE /api/todos/{id}` para TODO comum, sem parâmetro `scope`; para TODO recorrente, inicia `DELETE /api/todos/{id}?scope=occurrence`. Não renderizar modal/confirmação e não chamar `DELETE /api/todo-series/{seriesId}` por essa ação.
- Durante a requisição, impedir envio duplicado para o mesmo item e sinalizar que a exclusão está em andamento. Após resposta `204`, remover somente o ID selecionado do estado e da tela e sincronizar com a API; a série e as outras ocorrências permanecem.
- Em `400`, `401`, `404`, `409` ou falha de rede, manter o item na lista, encerrar o estado de envio e apresentar a mensagem de erro existente. O fluxo de `401` continua seguindo a regra de expiração da sessão.

### Critérios de aceite verificáveis

1. Teste de componente clica em Excluir para um TODO comum e verifica chamada imediata de `DELETE /api/todos/{id}` sem `scope`, sem que seja criado um modal/diálogo. O item permanece até a resposta `204`, então desaparece e a lista é sincronizada com a API.
2. Teste de componente clica em Excluir para ocorrência recorrente e verifica chamada imediata de `DELETE /api/todos/{id}?scope=occurrence`; nenhuma escolha de ocorrência/série ou confirmação aparece. Após `204`, só a ocorrência selecionada é removida, mantendo a série/itens irmãos.
3. Testes de serviço continuam verificando contratos da API para endpoint de série, se esse serviço é usado por outros fluxos; a ação Excluir na lista não chama a rota de exclusão integral de série.
4. Testes de falha verificam respostas `404`/`409` e falha de rede: a requisição termina, o item permanece na tela e uma mensagem de erro é apresentada. O comportamento de `401` continua seguindo a regra de expiração de sessão.
5. Testes de carregamento iniciam com uma coleção contendo IDs A/B e depois recebem uma resposta API contendo apenas B; após conclusão, só B aparece. Re-renderizar ou recarregar a mesma página não cria duplicatas nem conserva A.
6. Teste paginado com mais de 15 tarefas verifica concatenação das páginas conforme o contrato e ausência de duplicatas; depois de excluir um item e carregar novamente, a tela coincide com os registros efetivamente retornados pela API.
7. Os testes confirmam que não há mock, fixture, item otimista persistente nem tarefa hardcoded renderizada como dado real na tela de produção. Testes podem usar fixtures isoladas, sem vazamento para o estado real do componente.

### Limites e questões para revisão

- Não mudar contrato ou comportamento do backend sem evidência. A API é a fonte de verdade para existência e exclusão de TODOs.
- O serviço atual percorre as páginas e a página substitui seu estado no sucesso do carregamento; o mecanismo que produz o item inesperado deve ser localizado por testes/inspeção antes de escolher correção específica.
- A API também suporta exclusão integral de série por outra rota, mas a ação Excluir da lista não usa esse comportamento; nesta interface somente a ocorrência selecionada é excluída.

## Janela de agendamento e fuso horário

`dataInicioUtc` e `dataFimUtc` são uma janela de agendamento distinta de `dataPrevistaUtc`. Os dois limites são opcionais somente como par: enviar ambos com timestamp ISO 8601 e `Z`/offset, ou ambos como `null`. A resposta retorna instantes UTC; a interface deve formatar usando a preferência IANA da conta. A janela deve terminar depois do início e cair na mesma data local do usuário. Janela parcial, invertida/igual ou atravessando a meia-noite local retorna `400`; sobreposição retorna `409`. Intervalos são semiabertos `[início, fim)`, então uma janela pode começar exatamente quando outra termina.

Carregar `GET /api/preferences` antes de interpretar ou agendar horas locais. O padrão da conta é `America/Fortaleza`; salvar alteração por `PUT /api/preferences` com `{ timeZoneId }`. Exibir o fuso usado nos horários. Identificador IANA inválido retorna `400`; conflito ao mudar fuso retorna `409` e a preferência anterior permanece ativa. Instantes UTC já salvos não mudam quando o fuso é alterado.

Em recorrências com janela, manter horários de parede da âncora e aplicar os períodos futuros no calendário local do fuso IANA. A ocorrência inicial/conversão ancora na ocorrência `#1`; editar uma ocorrência `#N` com janela cria âncora `#N`, e a próxima projetada é `#N+1` um período depois. Recorrência diária avança um dia, semanal sete dias e mensal um mês com ajuste para o último dia existente. Em transição DST, horário local inexistente avança pelo tamanho do gap; horário ambíguo usa a primeira ocorrência cronológica. Conflitos com janela persistida ou slot futuro projetado devem preservar formulário/valores e exibir o erro `409` retornado pela API.

Ao editar uma ocorrência recorrente, informar antes de salvar que alterar a janela da ocorrência `#N` define uma nova âncora `#N` e altera as janelas projetadas para ocorrências futuras. Janelas das demais ocorrências já geradas não são recalculadas; a janela da ocorrência em edição é substituída pelos valores enviados. O aviso deve ser visível no formulário de edição, junto ao aviso de que título, descrição, categoria, prioridade e recorrência afetam ocorrências futuras.

## Rotas, páginas e componentes Angular

Manter `App` como componente raiz mínimo cujo template contém apenas `<router-outlet />`. Separar telas, templates e responsabilidades em componentes Angular próprios:

| Rota | Página/componente | Acesso e comportamento |
| --- | --- | --- |
| `/login` | página de login | pública; autentica pela API e, em sucesso, navega para `/todos` |
| `/register` | página de cadastro | pública; cria conta e depois direciona a pessoa para `/login` |
| `/todos` | página/lista de tarefas | protegida; carrega dados autenticados e apresenta lista/estados |
| `/todos/new` | página de formulário TODO reutilizável em modo criação | protegida; no sucesso de POST navega para `/todos` |
| `/todos/:id/edit` | a mesma página/componente de formulário em modo edição | protegida; carrega TODO por ID, no sucesso de PUT navega para `/todos`, e `404` volta à lista com aviso |

Formulário de criação/edição deve ter componente e template separados da lista e ser reutilizado pelas duas rotas por parâmetro de modo/ID. Ações de nova tarefa e editar navegam às páginas correspondentes; não abrir formulário em modal/drawer. Excluir TODO é imediato, sem diálogo de confirmação; para item recorrente, excluir somente a ocorrência selecionada. Rotas protegidas usam guard e resolução de sessão/dados necessários antes de apresentar o conteúdo. Compatibilidade SSR é obrigatória: não acessar `sessionStorage` nem APIs exclusivas do browser no servidor; a restauração da sessão e os redirects devem ocorrer no browser após a sessão ser verificada, sem considerar a ausência de storage no SSR como logout definitivo.

## Autenticação e configuração de conexão

Rotas de leitura/escrita de tarefas exigem JWT. A interface precisa permitir login e cadastro para obter o token antes de abrir a área protegida. Decisão de implementação: guardar o JWT em `sessionStorage`, acessado somente no browser por abstração com verificação de plataforma; não acessar APIs de armazenamento durante SSR. Isso limita a sessão à aba e permite renderizar no servidor a casca desautenticada. Sair ou receber `401` deve limpar a sessão local. Requisições subsequentes devem incluir o token Bearer. `sessionStorage` é acessível a scripts da página; a implementação deve evitar injeção de conteúdo não confiável e nunca registrar o token em logs.

Decisão de conexão: configurar o cliente HTTP com base relativa `/api`, centralizada em um único provider/config. No desenvolvimento, o servidor Angular deve usar proxy para encaminhar `/api` ao endereço `http://localhost:5091` observado no perfil HTTP de `TodoAPI/Properties/launchSettings.json`. Iniciar a API com `dotnet run --launch-profile http`; o perfil `https` aplica `UseHttpsRedirection()` e redireciona para `https://localhost:7054`, o que pode fazer o navegador sair da origem do proxy. Em produção, o reverse proxy que serve o Angular/SSR deve encaminhar `/api` à API na mesma origem. A API não registra CORS; com esse arranjo same-origin o navegador não precisa de CORS. O endereço direto `localhost` é somente o destino do proxy de desenvolvimento, nunca URL embutida no código de produção.

## Estrutura Angular e testes

Organizar arquivos por responsabilidade e por função da tela, migrando a estrutura plana atual em `src/app/pages/` e movendo os modelos hoje localizados em `core/`. A estrutura de destino é:

```text
src/app/
  app.ts
  app.html
  app.css
  app.routes.ts
  core/
    services e estado de sessão (auth, TODO, categorias, preferências)
    auth.guard.ts
    auth.interceptor.ts
    scheduling-time.ts
  models/
    todo.models.ts
  pages/
    login/
      login.page.ts
      login.page.html
      login.page.spec.ts
    register/
      register.page.ts
      register.page.html
      register.page.spec.ts
    todos/
      todos.page.ts
      todos.page.html
      todos.page.spec.ts
    todo-form/
      todo-form.page.ts
      todo-form.page.html
      todo-form.page.spec.ts
```

`core/` mantém serviços injetáveis de autenticação, TODO, categoria e preferência, armazenamento/estado da sessão, guard, interceptor e utilitários compartilhados. `models/` contém apenas modelos/interfaces de domínio e DTO, inclusive os tipos de TODO, categoria, autenticação, paginação e preferência. Componentes, templates e testes de cada tela ficam juntos na respectiva pasta `pages/<função>/`; a rota `/todos/new` e `/todos/:id/edit` reutilizam a página `todo-form`. Testes que cobrem comportamento exclusivo de uma página devem ficar no spec dessa página; testes de serviços permanecem junto ao serviço em `core/`. O componente raiz mantém apenas o `router-outlet`, sem formulário, tela ou lógica de negócio.

Manter componentes responsáveis pela apresentação/formulários e serviços injetáveis responsáveis por HTTP/autenticação. Centralizar tratamento de autorização e cabeçalho Bearer (por interceptor), sem acoplar a página aos detalhes de transporte. Configurar `HttpClient` no provider Angular. Respeitar SSR/hidratação existente, com APIs exclusivas do browser protegidas por abstração/plataforma.

### Correção dos textos visíveis

Revisar os textos em português apresentados pela interface, corrigindo caracteres corrompidos por mojibake e ortografia/acentuação do português brasileiro. Isso inclui textos de templates e mensagens de estado/erro escritos no TypeScript. Exemplos atualmente observados a corrigir: `TÃ­tulo` → `Título`, `ConcluÃ­das` → `Concluídas`, `PreferÃªncias` → `Preferências`, `horÃ¡rio` → `horário`, `sÃ©rie` → `série`, `MÃ©dia` → `Média` e separadores corrompidos `Â·`, `â€“`, `â€¦` para caracteres corretos ou pontuação equivalente. Revisar também rótulos, placeholders, mensagens, nomes acessíveis, estados vazios/carregando e confirmação.

Alterar somente textos apresentados à pessoa usuária. Preservar exatamente nomes de propriedades, valores enum, rotas e contratos da API, como `titulo`, `prioridade: "Media"`, `frequenciaRecorrencia: "Diaria"`, `timeZoneId`, `dataCriacao` e caminhos HTTP; a grafia interna desses identificadores não é texto de interface e não deve ser traduzida.

Critérios de aceite verificáveis:

1. A aplicação apresenta a lista em desktop e mobile, com paleta e tipografia do design system, sem dados hardcoded apresentados como tarefas reais. `App` contém somente `<router-outlet />`; login, cadastro, lista e formulário TODO são páginas/componentes separados nas rotas `/login`, `/register`, `/todos`, `/todos/new` e `/todos/:id/edit`.
2. Cadastro/login usam páginas e rotas próprias; rotas de tarefas são protegidas por guard/resolução SSR-safe. Uma sessão autenticada obtém tarefas com Bearer, e falha de autenticação/expiração é comunicada sem deixar a área protegida em estado falso de sucesso.
3. Criar e editar navegam para páginas com um formulário reutilizado; concluir/reabrir e excluir tarefa afetam a API. A lista é atualizada após sucesso, e erros não removem/alteram silenciosamente dados exibidos.
4. Campos do formulário incluem título, descrição, categoria opcional, prioridade e janela agendada opcional; janela envia ambos os limites como instantes com offset ou ambos nulos. Respeitam validações de data local e limites de recorrência por frequência. Preferência IANA é carregada antes de exibir/agendar horas e pode ser atualizada pela API; conflito preserva o fuso anterior. Categorias são carregadas e gerenciadas por API. Exclusão de tarefa recorrente pela lista remove somente a ocorrência selecionada com `scope=occurrence`, sem escolha de escopo ou encerramento da série.
5. A lista trata paginação acima de 15 tarefas, estados vazio/carregando/erro e respostas `400`, `401`, `404` e `409` relevantes.
6. Projeto, notificação, escrita de data de vencimento, busca e filtro “Hoje/Próximas” não são apresentados como funcionais sem extensão documentada da API. Categoria e prioridade usam exclusivamente os campos e rotas existentes.
7. Cada card sempre apresenta `dataCriacao` com data e hora no fuso da conta. Quando houver janela, exibe data e hora completas de início/fim no mesmo fuso. Se `categoriaId` existir e `categoriaNome` vier ausente, o nome exibido é resolvido das categorias já carregadas.
8. Testes verificam serviços HTTP de TODO, categorias e preferências (métodos, rotas, Bearer, serialização, 204 e erros 400/401/404/409) e, nos componentes:
   - rotas/componentes: `App` renderiza apenas router outlet; `/login`, `/register`, `/todos`, `/todos/new` e `/todos/:id/edit` apresentam páginas separadas; guard não acessa storage no SSR e restaura sessão somente no browser;
   - formulário de criação: título e descrição válidos enviam `POST`; cada campo vazio/apenas espaços ou acima de 200 caracteres impede envio;
   - formulário de edição: preenche os dados da tarefa, envia `PUT` com os campos editáveis e reflete sucesso; erro de validação mantém o formulário e exibe falha;
   - exclusão de TODO: clique em Excluir chama imediatamente a rota de tarefa; em recorrência envia `scope=occurrence`, remove somente a ocorrência selecionada e preserva a série. Não há diálogo, escolha de escopo ou chamada à rota de exclusão integral da série;
   - estado carregando é exibido durante a requisição pendente e removido após resposta; falha de carregamento/API exibe estado de erro sem substituir a lista por sucesso vazio;
   - janela/fuso: carrega e atualiza `/api/preferences`, envia ambas as datas ou ambas nulas, trata validação 400 e conflito 409 mantendo valores digitados, e apresenta horários no fuso da conta; recorrência editada na ocorrência #2 mostra aviso de nova âncora, usa essa ocorrência como âncora e projeta a #3 um período depois sem recalcular janelas das demais ocorrências já geradas;
   - cards: formata data de criação e janela com data+hora no fuso IANA, e resolve categoria pelo ID quando `categoriaNome` não vier na resposta;
   - responsividade estrutural: em viewport mobile são apresentados cabeçalho compacto, navegação inferior e botão flutuante de criação; em desktop aparece a navegação lateral, sem overflow horizontal da página em largura mobile.
9. A estrutura final agrupa os arquivos conforme o bloco acima: modelos sob `src/app/models/`, utilitários/serviços/guard/interceptor/sessão sob `src/app/core/` e cada página com TypeScript, template HTML e spec sob sua pasta funcional. Imports e testes compilam usando os novos caminhos; não ficam cópias antigas dos mesmos componentes/modelos nas pastas anteriores.
10. As rotas `/login`, `/register`, `/todos`, `/todos/new` e `/todos/:id/edit` apontam para as páginas nas novas pastas, mantendo os dois modos do formulário reutilizável e os guards atuais. O template raiz segue contendo somente `<router-outlet />`.
11. Revisão de todos os textos visíveis de login, cadastro, lista, formulário, categorias, preferências, carregamento, vazio, confirmação e erro não encontra sequências de mojibake conhecidas (`Ã`, `Â·`, `â€“`, `â€¦`) nem erros de acentuação em português. Testes das páginas verificam ao menos os rótulos e mensagens com acentos relevantes. Identificadores e valores JSON/API permanecem sem alterações.

## Configuração e limites de ambiente

- Limites de recorrência incluem a ocorrência inicial: diária `1–3650`, semanal `1–520`, mensal `1–120`; em série existente, a duração não pode ficar abaixo da maior ocorrência já gerada. A interface deve validar os limites e mostrar `400` do backend sem perder valores.
- Os limites mínimo/máximo de duração de uma janela não estão especificados; não inventar limites. O backend valida paridade, ordem, mesmo dia local e conflitos projetados.
- A publicação depende de o proxy de produção encaminhar `/api` para a API. A API não registra política CORS; não configurar chamada direta cross-origin sem primeiro alterar e documentar a política do backend.
- A API oferece lista paginada, CRUD de categorias, prioridade, janelas por TODO e preferência IANA, mas não filtros de servidor nem endpoint de calendário agregado. “Hoje”, “Próximas”, projetos, busca ou uma agenda agregada exigiriam contratos backend adicionais. Esta especificação limita a interface funcional aos dados existentes até haver decisão explícita de ampliar a API.
