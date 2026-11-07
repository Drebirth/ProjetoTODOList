# TodoAPI e Front-TodoApp

Aplicação de lista de tarefas com API ASP.NET Core, interface Angular e persistência SQLite. A interface se conecta à API pelo proxy do Angular durante o desenvolvimento.

## Requisitos

- .NET SDK 10
- Node.js e npm compatíveis com Angular CLI 21 (o projeto declara `npm@10.9.2`)
- Git, para clonar o repositório

## Inicialização para desenvolvimento

Abra dois terminais na raiz do repositório.

### 1. Iniciar a API

No primeiro terminal:

```powershell
cd TodoAPI
dotnet run --launch-profile http
```

A API inicia em `http://localhost:5091`. No ambiente Development, o Swagger fica em `http://localhost:5091/swagger`.

O Entity Framework aplica as migrations ao iniciar. O SQLite usa a connection string `TodoDatabase` de `TodoAPI/appsettings.json`, com o arquivo `todoapi.db` no diretório de execução da API.

### 2. Iniciar o frontend

No segundo terminal, a partir da raiz:

```powershell
cd Front-TodoApp
npm install
npm start
```

Abra `http://localhost:4200`. O proxy em `Front-TodoApp/proxy.conf.json` encaminha as requisições `/api/**` para `http://localhost:5091`; por isso, inicie a API antes de usar o frontend.

Para encerrar cada processo, pressione `Ctrl+C` no terminal correspondente.

## Primeiro acesso

1. Abra o frontend em `http://localhost:4200`.
2. Crie uma conta na tela de cadastro.
3. Entre com o nome de usuário e a senha escolhidos.
4. Crie e organize tarefas. A API também cria categorias iniciais para a conta.

## Testes e build

Na raiz do repositório, execute os comandos da área desejada:

```powershell
# Testes da API
dotnet test TodoAPI.Tests/TodoAPI.Tests.csproj

# Testes do Angular
cd Front-TodoApp
npm test -- --watch=false

# Build de produção do Angular
npm run build
```

## Estrutura do repositório

- `TodoAPI/` — API ASP.NET Core, autenticação JWT, regras de tarefas e recorrência, migrations e SQLite.
- `TodoAPI.Tests/` — testes automatizados da API.
- `Front-TodoApp/` — aplicação Angular e configuração do proxy local.
- `documentação/backend/` — contratos e documentação da API.
- `documentação/frontend/` — documentação de interface e integração com a API.

## Configuração e segurança

- A URL do proxy do frontend é `http://localhost:5091`; se mudar a porta da API, ajuste `Front-TodoApp/proxy.conf.json`.
- A connection string pode ser alterada em `TodoAPI/appsettings.json` ou por configuração do ASP.NET Core.
- `TodoAPI/appsettings.Development.json` contém uma chave JWT destinada somente ao desenvolvimento local. Configure uma chave segura por variável de ambiente ou secret manager antes de publicar a API; não reutilize a chave local em produção.

Mais detalhes sobre rotas e payloads estão em [`documentação/backend/listas-e-autenticacao/README.md`](documentação/backend/listas-e-autenticacao/README.md) e [`documentação/frontend/integracao-api-todo/README.md`](documentação/frontend/integracao-api-todo/README.md).
