# Swagger para a API

## Objetivo

Disponibilizar documentação interativa da API backend para consulta e chamadas locais durante o desenvolvimento.

## Estado atual observado

- O backend usa .NET 10, registra controllers e configura `AddOpenApi()`.
- Em `Development`, `MapOpenApi()` publica o documento em `/openapi/v1.json`.
- A interface Swagger UI ainda não está configurada.
- `AuthController` expõe cadastro e login em `/api/auth`; essas rotas são públicas.
- `TodosController` usa `[Authorize]`; todas as operações em `/api/todos` exigem JWT Bearer.
- Os perfis de execução local definem `Development`, com `http://localhost:5091` e `https://localhost:7054`.

## Requisitos

- Disponibilizar Swagger UI em `/swagger` somente quando o ambiente for `Development`.
- Apresentar um único documento OpenAPI, versão `v1`, contendo as operações dos controllers de autenticação e tarefas e seus contratos de requisição/resposta.
- Documentar JWT Bearer como esquema de segurança e associá-lo apenas às operações protegidas de `/api/todos`. Cadastro e login devem aparecer como operações públicas.
- Permitir autenticar chamadas da interface com um token obtido por `POST /api/auth/login`, sem exigir o prefixo `Bearer ` no campo de entrada quando a UI já constrói o cabeçalho.
- Manter o JSON OpenAPI acessível para a interface em `/openapi/v1.json` ou em uma rota equivalente documentada.
- Não expor Swagger UI nem o documento OpenAPI em ambientes que não sejam `Development`.

## Uso local esperado

1. Inicie o backend com um dos perfis locais `http` ou `https`, que definem o ambiente como `Development`.
2. Acesse `http://localhost:5091/swagger` ou `https://localhost:7054/swagger`.
3. Registre uma conta e faça login nas operações de `/api/auth`.
4. Use o token JWT retornado no controle **Authorize** para testar operações de `/api/todos`.

O backend também exige `Jwt:SigningKey` com pelo menos 32 bytes. Em desenvolvimento, o projeto fornece esse valor em `appsettings.Development.json`, que é configuração local ignorada pelo Git; fora desse ambiente, a chave deve ser configurada por uma fonte segura do ambiente de execução.

## Abordagem técnica proposta

O projeto já gera OpenAPI com `Microsoft.AspNetCore.OpenApi`. Preferir conectar uma Swagger UI a esse documento e adicionar a metadata de segurança JWT ao documento, evitando manter dois geradores concorrentes. Geto deve validar a compatibilidade dessa abordagem com as operações MVC/controllers e com a versão atual do .NET antes da implementação. Se houver limitação, documentar a alternativa técnica escolhida antes de Gojo alterar o código.

## Critérios de aceite

1. Em `Development`, `/swagger` retorna a interface e ela carrega o documento `v1` sem erro.
2. O documento inclui as rotas `POST /api/auth/register`, `POST /api/auth/login` e todas as operações CRUD em `/api/todos`.
3. A interface mostra o esquema HTTP Bearer JWT e permite enviar o token nas operações protegidas.
4. Operações de cadastro e login não são marcadas como exigindo autenticação.
5. Fora de `Development`, tanto `/swagger` quanto o endpoint do documento OpenAPI não são servidos.
6. Os schemas de request/response refletem os contratos existentes dos controllers.

## Fora do escopo

- Publicar a documentação em produção ou proteger sua interface com um segundo mecanismo de autenticação.
- Alterar autenticação, contratos, regras de negócio ou endpoints existentes da API.
