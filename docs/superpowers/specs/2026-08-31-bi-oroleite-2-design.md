# BI OROLEITE 2.0 - Design de Migracao

## Objetivo

Migrar o BI comercial hoje concentrado em `index.html` para uma aplicacao web modular, segura e pronta para producao no Azure. A primeira entrega deve reproduzir os resultados do legado a partir dos CSVs atuais antes de introduzir a sincronizacao com Firebird.

## Escopo da Primeira Entrega

- Login local por usuario e senha, gerenciado pelo BI.
- Login corporativo opcional por Microsoft Entra ID.
- Perfis internos `Administrador`, `Gestor` e `Vendedor`.
- Importacao auditavel de `POWER.csv`, `PPP.csv`, `METAS.csv` e `VALOR_METAS.csv`.
- Modulos: Dashboard, Visao de Trocas, Analise Venda x Troca, Margem de Produtos e Fechamento por Vendedor.
- Persistencia no banco de dados e validacao de paridade com o legado.

Ficam fora desta entrega a sincronizacao efetiva com Firebird e novas regras comerciais. A abstracao para a futura fonte Firebird fara parte do desenho tecnico.

## Arquitetura

O frontend sera uma SPA React com TypeScript, criada com Vite e hospedada no Azure Static Web Apps. A API sera ASP.NET Core Web API, hospedada no Azure Container Apps no perfil Consumption. Importacoes agendadas ou de maior duracao usarao Azure Container Apps Jobs.

O codigo sera dividido nos projetos abaixo:

```text
src/
  OroBI.Domain/          Entidades, valores e contratos de dominio
  OroBI.Application/     Casos de uso, calculos, DTOs e interfaces
  OroBI.Infrastructure/  EF Core, Identity, Blob Storage e implementacoes externas
  OroBI.Api/             HTTP, autenticacao, autorizacao e composicao da aplicacao
  OroBI.Web/             React, telas, filtros e visualizacao
tests/
  OroBI.Domain.Tests/
  OroBI.Application.Tests/
  OroBI.Api.IntegrationTests/
```

As regras de negocio pertencem a `Application` e nao dependem de HTTP, banco ou componentes React. A API apenas autoriza, valida a entrada e delega os casos de uso. Essa separacao permite testar e substituir a origem dos dados sem alterar os calculos.

## Infraestrutura Azure

- Azure Static Web Apps para o frontend.
- Azure Container Apps Consumption para a API, com escala minima zero enquanto a operacao permitir.
- Azure Container Apps Jobs para importacoes e futura sincronizacao.
- Azure Database for PostgreSQL Flexible Server como banco PaaS.
- Azure Blob Storage para arquivos de importacao originais e seus relatorios.
- Azure Key Vault para segredos excepcionais.
- Application Insights e Log Analytics para telemetria e logs estruturados.

O PostgreSQL usara rede privada e nao tera endpoint publico. A API acessara servicos Azure por Managed Identity. O custo inicial sera otimizado com PostgreSQL burstable B1ms, sujeito a confirmacao de preco na regiao escolhida antes do provisionamento.

## Autenticacao e Autorizacao

O sistema aceita dois provedores de identidade:

1. ASP.NET Core Identity para usuarios locais, com senha armazenada como hash seguro e fluxo de recuperacao de senha.
2. Microsoft Entra ID por OpenID Connect para usuarios corporativos.

Ambos os provedores se vinculam a um usuario interno unico. Os perfis e os escopos pertencem ao BI e nao ao provedor de login.

- `Administrador`: usuarios, perfis, configuracoes, importacoes e visao completa.
- `Gestor`: consulta os dados liberados para sua area e equipe.
- `Vendedor`: consulta somente seus resultados e seu fechamento.

A API emitira tokens locais de curta duracao e aplicara autorizacao por perfil e por escopo de vendedor. O acesso tecnico ao banco e aos recursos Azure nao depende de contas dos usuarios.

## Modelo de Dados e Importacao

Cada upload cria um lote de importacao com usuario, horario, tipo de arquivo, checksum, status, totais, erros e referencia ao arquivo original no Blob Storage. Linhas normalizadas mantem a referencia ao lote para auditoria e possibilidade de reprocessamento.

As entidades iniciais incluem:

- `ImportBatch` e `ImportError` para a trilha de importacao.
- `CommercialMovement` para linhas de `POWER`.
- `PppRecord`, `GoalRecord` e `GoalValueRecord` para PPP e metas.
- `ApplicationUser`, `Role` e vinculos de identidade externa.
- Configuracoes de fechamento por vendedor e mes, quando a fonte CSV nao as determinar.

O parser tratara CSV separado por ponto e virgula, UTF-8 e Windows-1252, validando colunas obrigatorias, datas, numeros, chaves e duplicidades. Falhas de linha nao corrompem lotes ja concluidos; o lote fica com status apropriado e relatorio acionavel pelo usuario.

## Regras Comerciais Preservadas

As regras atuais serao reimplementadas com os mesmos nomes e criterios antes de qualquer evolucao:

- Venda bruta: soma de `VALTOTAL` em `TIPO = VENDA`.
- Movimentos negativos: soma do valor absoluto de linhas com valor negativo.
- Resultado liquido: soma de todos os movimentos.
- Trocas fisicas: `TROCA` e `TROCA DEV`.
- Receita da analise Venda x Troca: `VENDA`, `DEVOL ENT` e `DEVOLUCAO` respeitando o sinal.
- Margem: receita de venda menos `QTDE * PRECOCUSTO`.
- PPP, metas, premios, comissao e fechamento reproduzem os limiares e formulas do legado.

Os servicos de aplicacao aceitam filtros de periodo, vendedor, marca, grupo, cidade, cliente, produto e tipo de movimento. As dimensoes de agrupamento incluem vendedor, marca, cliente, produto, cidade, tipo, grupo, familia e data, conforme o modulo.

## Abstracao de Fonte Comercial

`ICommercialDataSource` definira a leitura de movimentos comerciais e dos dados auxiliares. A primeira implementacao sera CSV persistido. A proxima sera Firebird, executada por job e gravando no mesmo modelo normalizado. As telas e os calculos nunca conhecem a origem dos dados.

## API Inicial

- `POST /api/auth/login` e fluxos de login externo.
- `GET /api/me` para perfil e escopos do usuario autenticado.
- `POST /api/imports` para receber arquivos e iniciar validacao.
- `GET /api/imports` e `GET /api/imports/{id}` para auditoria e erros.
- `GET /api/dashboard`, `/api/trades`, `/api/sales-trades`, `/api/margins` e `/api/closings` para analises filtradas.
- Endpoints administrativos para usuarios, perfis e configuracoes permitidas.

## Qualidade, Observabilidade e Aceite

Serao criados testes unitarios para cada formula, testes de importacao para arquivos validos, invalidos, duplicados e com codificacao distinta, e testes de integracao para autenticacao, autorizacao e endpoints criticos.

Para cada modulo, a aceitacao exige um relatorio de paridade comparando legado e novo sistema no mesmo conjunto de CSVs, por periodo, vendedor, marca e tipo de movimento. Diferencas devem ser classificadas como erro, dado de origem ou regra explicitamente aprovada.

Logs estruturados nao devem registrar senha, token ou dados sensiveis desnecessarios. Falhas da API retornam erros padronizados e rastreaveis por identificador de correlacao.

## Sequencia de Entrega

1. Criar a solucao modular, configuracoes locais e fundamentos de autenticacao.
2. Modelar banco, migrations, auditoria e importacao de CSV.
3. Reimplementar e testar regras comerciais com paridade do legado.
4. Expor API e construir as telas React dos cinco modulos.
5. Provisionar infraestrutura Azure por infraestrutura como codigo e configurar telemetria.
6. Adicionar adaptador Firebird e job de sincronizacao apos a paridade da fonte CSV.
