# Instruções do projeto TodoAPI

## Agentes obrigatórios

Use os três agentes definidos em `.codex/agents/` em toda nova funcionalidade ou alteração que envolva documentação e código. Coordene o trabalho nesta ordem:

1. **Shoko** (`shoko`): analisa o comportamento atual e cria ou atualiza a documentação antes da implementação.
2. **Geto** (`geto`): revisa a documentação da Shoko em busca de inconsistências, ambiguidades, lacunas e critérios não verificáveis. Se encontrar problemas, devolve-os à Shoko para correção e revisa novamente.
3. **Gojo** (`gojo`): implementa o código de acordo com a documentação aprovada. Escreve os testes primeiro (TDD), confirma que falham pelo motivo esperado quando possível e então implementa até passarem.
4. **Geto**: depois da implementação, executa os testes escritos pelo Gojo e relata os comandos executados e resultados. Não declare os testes aprovados sem evidência observada.

Não inicie a implementação antes da revisão da documentação. Se a documentação aprovada não definir um requisito necessário, registre a lacuna e peça que Shoko/Geto a resolvam antes de Gojo implementar.

## Documentação

- Guarde a documentação do projeto na pasta `documentação/` na raiz, separada por área: `documentação/backend/` para a API ASP.NET Core e `documentação/frontend/` para o Angular.
- Para uma funcionalidade nova, crie dentro da área correspondente uma subpasta com um nome curto que identifique a funcionalidade ou alteração e mantenha nela os documentos relacionados.
- Para alterações de uma funcionalidade já documentada, atualize os documentos existentes na subpasta correspondente; evite criar documentos duplicados ou desnecessários.
- A documentação deve ser suficiente para orientar implementação e revisão: objetivo, comportamento esperado, contratos/interfaces afetados, critérios de aceite e casos de borda relevantes. Não invente requisitos ausentes; marque-os como pendentes.
- Geto deve apontar inconsistências com referências às seções relevantes. Shoko corrige os documentos; Geto confirma a resolução antes da implementação.

## Implementação e qualidade

- Siga SOLID, TDD, POO e boas práticas compatíveis com a estrutura e tecnologias já adotadas pelo projeto.
- Mantenha controllers focados em HTTP e delegue regras de negócio a serviços. Separe responsabilidades em classes coesas e use injeção de dependência conforme os padrões existentes.
- Prefira a menor alteração coerente com a documentação. Não introduza padrões, dependências ou abstrações sem necessidade demonstrável.
- Cubra comportamento novo e regressões relevantes com testes. Geto executa os testes escritos por Gojo e relata falhas sem ocultá-las.
- Não altere arquivos alheios à funcionalidade em andamento.

## Git

- Para cada nova funcionalidade concluída, execute `git add .` e crie um commit com `git commit -m "<nome da funcionalidade>"`, usando uma mensagem curta que identifique a funcionalidade.
- Antes de adicionar arquivos, confira o estado do repositório e não inclua alterações preexistentes que não pertençam à funcionalidade. Se houver alterações alheias, preserve-as e informe o impedimento antes do commit.
- Não faça commit de uma funcionalidade com documentação ainda não revisada ou testes pendentes/falhando, salvo decisão explícita do usuário.

## Estado atual

- A solução contém o projeto ASP.NET Core em `TodoAPI/` e o frontend em `Front-TodoApp/`.
- Preserve as convenções e dependências existentes em cada parte do projeto. Inspecione os arquivos relevantes antes de propor mudanças arquiteturais.
