# BI OROLEITE 2.0 - Backlog Mestre

Este arquivo e o ponto unico de acompanhamento dos prompts do projeto. Ele registra o estado observado no repositorio, a proxima acao concreta e a evidencia associada. Os PDFs sao fontes de requisitos; este TODO nao altera suas instrucoes.

## Ultima alteracao

| Data | Item | Alteracao | Impacto |
| --- | --- | --- | --- |
| 2026-09-02 | Proxima prioridade agendada | Retomar a engenharia reversa integral do `index.html` assim que o deploy e o provisionamento dos administradores em curso forem concluídos. | Catalogar módulos, regras e CSVs do legado; criar baselines de paridade antes de ampliar funcionalidades comerciais. |
| 2026-09-02 | Static Web App OroBI | Criado `orobi-web` em `rg-oroleite-site`, com hostname `lively-sea-0776c9a0f.6.azurestaticapps.net`, sem alterar o legado `orlt-bi`. | A publicacao React depende da configuracao do token no GitHub e do workflow com gate de testes; CORS permanece inalterado ate a SPA estar publicada. |
| 2026-09-02 | Deploy e migracao Azure | Infraestrutura aplicada com imagem `orobi-api:20260901.2`, referencia de segredo e CORS publicados; job manual de migracao concluiu. | API em execucao, health check `200` e banco migrado. A proxima verificacao e funcional, pela SPA publicada. |
| 2026-09-02 | Protecao do deploy Azure | `deploy-azure.ps1` passou a exigir imagem, origem CORS e referencia de segredo para qualquer `-Apply`. | O script nao pode mais aplicar defaults que revertam a imagem, removam CORS ou desativem a conexao de banco. |
| 2026-09-02 | Auditoria de deploy Azure | O workflow deixou de enviar `databaseConnectionString`, parametro inexistente no Bicep; teste operacional protege a regressao. | O deploy passa apenas os parametros declarados e foi validado localmente antes da aplicacao. |
| 2026-09-02 | Verificacao local | Pester operacional, compilacao Bicep, suites .NET Release, testes e build Web foram reexecutados. | Validacao local atual: 8 + 1 + 2 + 2 testes Pester, 48 testes .NET, 7 testes Web e build Web sem falhas. |
| 2026-08-31 | Entrega segura de segredos Azure | Bicep separa identidade/RBAC da referencia de segredo; bootstrap grava segredos no Key Vault e deploy usa referencia ARM nao secreta. | Restam valores reais, `what-if`, deploy base e ativacao runtime apos propagacao RBAC. |
| 2026-08-31 | Verificacao final disponivel | Suites .NET Release, testes Web e build Web executados apos API v1. | Plataforma validada localmente; pendencias restantes sao Azure autenticacao, paridade e Firebird. |
| 2026-08-31 | Dados Firebird | Confirmada indisponibilidade atual de host, credenciais, consulta e watermark. | Integracao Firebird fica para a etapa final; a plataforma continua CSV/PostgreSQL com API v1. |
| 2026-08-31 | API v1 | Adicionadas rotas `/api/v1` para autenticacao, usuario, importacao, dashboard, analises e fechamento, mantendo `/api` compativel. | A Fase 2 possui superficie versionada para clientes mobile; 8 testes de integracao passaram. |
| 2026-08-31 | Bootstrap de segredos Azure | Fase adiada por solicitacao do usuario devido a problemas de autenticacao e RBAC. | O Key Vault `orobikv` permanece criado; retomada exige acesso de dados `Key Vault Secrets Officer` para a conta de deploy. |
| 2026-08-31 | Key Vault | Criado `orobikv` com RBAC e purge protection; a conta autenticada nao pode criar role assignments. | Um administrador deve conceder `Key Vault Secrets Officer` no cofre ou `User Access Administrator` temporario antes da geracao dos segredos. |
| 2026-08-31 | Pre-deploy Azure | Adicionado `scripts/deploy-azure.ps1`, com validacao dos dois segredos e `what-if` por padrao; `-Apply` exige acao explicita. | O deploy pode ser repetido sem expor segredos em arquivos ou comandos manuais; teste Pester passou. |
| 2026-08-31 | Publicacao da API | Criado ACR Basic `orobiacr`, com usuario administrador desabilitado; imagem `orobi-api:20260831` publicada pelo build remoto `ch2`. | O deploy principal ainda depende de `POSTGRES_ADMINISTRATOR_PASSWORD` e `OROBI_DATABASE_CONNECTION_STRING`; `.dockerignore` protege builds futuros contra envio de cache Azure e CSVs. |
| 2026-08-31 | Registro de container | Consulta da assinatura nao encontrou Azure Container Registry existente. | A publicacao da API exige decidir entre criar um ACR pago, usar um registro externo ou configurar build/publicacao no CI. |
| 2026-08-31 | Diagnostico Azure | Assinatura `Empresas` autenticada e `rg-oroleite-site` inspecionado; ha dois Static Web Apps e uma conta de armazenamento, sem registro de container ou imagem da API. | O deploy exige publicar uma imagem acessivel para a API e configurar os segredos do workflow antes da criacao de recursos. |
| 2026-08-31 | Deploy Azure | Verificada a sessao local do Azure CLI; nao ha conta autenticada. | O deploy exige `az login` com permissao no grupo de recursos ou o workflow GitHub com identidade federada e segredos configurados. |
| 2026-08-31 | Verificacao backend | Suites API, Application e Infrastructure passaram em Release, com 44 testes no total. | Fundacao, persistencia, importacao, autenticacao, analytics e fechamento da Fase 1 possuem evidencia automatizada atual. |
| 2026-08-31 | SPA React | Cobertos login, importacao, dashboard, analise Venda x troca e fechamento; teste e build Web passaram. | A entrega da SPA da Fase 1 esta concluida com protecao dos fluxos principais. |
| 2026-08-31 | Cobertura SPA | Adicionado teste para a rota e metricas de Venda x troca. | A navegacao analitica agora tem protecao contra regressao de endpoint e de apresentacao. |
| 2026-08-31 | Empacotamento Azure | Bicep instalado localmente e `infra/main.bicep` compilado com sucesso. | A infraestrutura esta validada sintaticamente; o proximo passo e fornecer parametros e credenciais para deploy. |
| 2026-08-31 | Paridade comercial | Fase adiada por solicitacao do usuario. | A validacao contra baselines do legado permanece pendente de valores aprovados pelo negocio. |
| 2026-08-31 | Integracao Firebird | Fase adiada por solicitacao do usuario. | A entrega atual permanece CSV/PostgreSQL; retomada exige mapa de dados Firebird aprovado. |
| 2026-08-31 | Fundacao Firebird | Adicionados checkpoint, execucao de sincronizacao, chave de origem idempotente e migration PostgreSQL. | O modelo agora pode registrar e deduplicar futuras leituras Firebird. |
| 2026-08-31 | Catalogo Fase 2 | Registrados os requisitos confirmados no indice do adendo, sem inferir detalhes que nao puderam ser extraidos do PDF. | A proxima decisao tecnica da Fase 2 fica visivel e rastreavel. |
| 2026-08-31 | Verificacao .NET e infraestrutura | Suite .NET Release passou; validacao Bicep foi tentada com configuracao local, mas o Bicep nao esta instalado e o proxy `127.0.0.1:9` recusa a conexao necessaria para baixa-lo. | O codigo de aplicacao esta validado; a compilacao Bicep continua dependente de conectividade externa. |
| 2026-08-31 | Fechamento e filtros Web | Adicionada consulta por vendedor/mes e persistencia do filtro de vendedor na URL. | A tela de fechamento consome a API e o recorte do dashboard pode ser compartilhado. |
| 2026-08-31 | SPA React | Extraidos cliente HTTP, sessao, dashboard, importacao e telas analiticas; adicionados testes Vitest e build compativel com o ambiente. | A aplicacao agora tem modulos testaveis e navegacao para Dashboard, Trocas, Venda x Troca e Margem. |
| 2026-08-31 | Linha de base | Criado o backlog mestre a partir da documentacao existente e dos dois PDFs de referencia. | O proximo trabalho e identificavel sem procurar a ultima conversa ou alteracao. |

## Fontes de referencia

| Fase | Documento | Papel |
| --- | --- | --- |
| Fase 1 | [BI OROLEITE 2.0 - Mfase 1.pdf](</C:/Users/Tarcisio/OneDrive - Empresas/Área de Trabalho/BI OROLEITE 2.0 — Mfase 1.pdf>) | Requisitos e escopo inicial do BI. |
| Fase 2 | [Adendo ao Prompt - fase 2.pdf](</C:/Users/Tarcisio/OneDrive - Empresas/Área de Trabalho/Adendo ao Prompt — fase 2.pdf>) | Requisitos adicionais da segunda fase. |
| Plano tecnico | [Plano CSV Platform](superpowers/plans/2026-08-31-bi-oroleite-csv-platform.md) | Decomposicao tecnica atual em oito tarefas. |
| Design tecnico | [Design de migracao](superpowers/specs/2026-08-31-bi-oroleite-2-design.md) | Decisoes de arquitetura e escopo da primeira entrega. |

## Legenda

- `[x]` Implementado no repositorio e com evidencia registrada.
- `[~]` Parcialmente implementado ou em validacao.
- `[ ]` Pendente.
- `[!]` Bloqueado por dependencia, decisao ou evidencia ausente.

## Fase 1 - Plataforma CSV e BI

| Status | Entrega | Estado observado | Proxima acao | Evidencia / referencia |
| --- | --- | --- | --- | --- |
| `[x]` | Fundacao .NET e projetos em camadas | Solucao, projetos Domain/Application/Infrastructure/API e suites de teste existem. | Evoluir somente diante de novo requisito de plataforma. | Plano tecnico, tarefa 1; 44 testes Release. |
| `[x]` | Modelo comercial persistente | Entidades, `OroBiDbContext` e migrations possuem cobertura de infraestrutura. | Evoluir somente diante de novo requisito de dados. | Plano tecnico, tarefa 2; 13 testes Infrastructure. |
| `[x]` | Importacao CSV auditavel | Endpoint de importacao e workflow possuem testes; a SPA cobre o envio multipart administrativo. | Registrar baselines apenas se a paridade comercial for retomada. | Plano tecnico, tarefa 3; 44 testes backend e 7 Web. |
| `[x]` | Autenticacao e escopos | Endpoints de login e usuario atual possuem testes de integracao; a SPA cobre sessao valida. | Configurar identidade Entra somente durante o deploy Azure. | Plano tecnico, tarefa 4. |
| `[x]` | Analytics comercial | Endpoint de dashboard, calculos de aplicacao e navegacao Venda x troca possuem testes. | Retomar somente com baselines comerciais aprovados. | Plano tecnico, tarefa 5. |
| `[x]` | PPP, metas e fechamento | Endpoint de fechamento e calculadoras possuem testes de aplicacao e integracao. | Retomar somente com novas regras comerciais. | Plano tecnico, tarefa 6. |
| `[x]` | SPA React operacional | Cliente HTTP, sessao, dashboard, importacao, analises e fechamento foram separados em modulos; testes cobrem login, importacao, filtros, fechamento e Venda x troca. | Avaliar filtros adicionais por modulo apenas se houver novo requisito comercial. | Plano tecnico, tarefa 7. |
| `[!]` | Paridade comercial | Documentacao de paridade existe; a suite .NET Release passou. | Adiada por solicitacao do usuario; depende de baselines aprovados do legado. | Plano tecnico, tarefa 8. |
| `[x]` | Empacotamento Azure | ACR `orobiacr`, Key Vault `orobikv`, Container App `orobi-api` e job `orobi-migrate` foram aplicados. A revisao `orobi-api--0000007` usa `orobi-api:20260901.2`; a migracao manual concluiu. | Executar verificacao funcional pela SPA antes de nova publicacao. | What-if, deploy aplicado, health check `200` e execucao `orobi-migrate-sgt082o` concluida em 2026-09-02. |
| `[~]` | Publicacao da SPA Azure | Static Web App dedicado `orobi-web` criado em `lively-sea-0776c9a0f.6.azurestaticapps.net`; o legado `orlt-bi` permanece separado. | Configurar segredo de deployment no GitHub, publicar a SPA React e aplicar o hostname no CORS da API. | Plano `2026-09-02-orobi-web-static-app.md`, tarefa 1. |

### Proxima prioridade da Fase 1

1. `[~]` Assim que terminar o deploy e o provisionamento dos administradores em curso, analisar integralmente o `index.html` como documentação funcional do legado: módulos, regras, CSVs, campos, filtros e cálculos.
2. `[ ]` Criar baselines e testes de regressão que comparem os resultados do legado com o backend antes de ampliar módulos comerciais.
3. `[ ]` Retomar a integração Firebird somente com o mapa de dados, consulta aprovada e configuração da VM.

## Fase 2 - Adendo

Em 2026-09-05 foi extraído e lido o texto integral das 24 páginas do adendo (itens 68–106), usando `pdftotext -layout -enc UTF-8`. O registro de 31/08 se baseava apenas no índice. A tabela abaixo substitui aquela avaliação parcial. Esta leitura atende ao pedido de situação da fase 2; não autoriza por si só ativar a integração ou mudar a infraestrutura.

| Status | Requisito confirmado | Impacto no estado atual | Proxima acao |
| --- | --- | --- | --- |
| `[x]` | Frontend → API → banco BI; consultas sem acesso direto ao ERP (68–72, 90–94) | React consome a API; regras comerciais estão no backend; PostgreSQL é a base atual. | Preservar essa separação ao conectar o Firebird. |
| `[!]` | Adaptador Firebird somente leitura e proteção do ERP (73–75, 84–87) | Existe `IFirebirdCommercialReader`; não existe leitor conectado nem `ICommercialDataSource` comum às fontes CSV/Firebird. | Obter mapa real das tabelas, consulta aprovada, chave de origem, watermark e acesso somente leitura. |
| `[~]` | Checkpoint, histórico e idempotência (79–82) | Entidades `SynchronizationCheckpoint`/`SynchronizationRun`, persistência e chave de origem existem; não comprovam uma sincronização funcional. | Implementar serviço de upsert, transações e testes de reprocessamento. |
| `[!]` | Worker periódico, incremental e cancelamentos (76–78, 83) | Não existe projeto Worker no código; há design e plano. | Implementar após confirmar origem, regras de alteração/cancelamento e ambiente de execução. |
| `[~]` | CSV como contingência e comparação CSV × Firebird (88–89, 105) | Importação CSV operacional; valores comerciais têm verificações, mas nenhuma comparação com Firebird foi realizada. | Conciliar o mesmo período nas duas fontes antes de trocar a fonte principal. |
| `[x]` | API preparada para mobile e versionada | Rotas `/api/v1` existem para os endpoints comerciais, com aliases `/api` preservados. | Migrar consumidores para v1 somente quando houver validacao mobile. |
| `[ ]` | Última sincronização, painel administrativo e resiliência (96–100) | Não há sincronização Firebird a acompanhar; health da API não comprova saúde do Worker/ERP. | Implementar status, histórico, timeout e retry limitado junto à integração. |
| `[~]` | Deploy, rede e segredos (101–104) | API/Web em Azure; design existente prevê Worker na rede do ERP e mantém API/Web em Azure. O PDF apresenta VM como recomendação. | Confirmar conectividade da VM e PostgreSQL, sem expor o ERP nem mover a aplicação automaticamente. |
| `[~]` | Resultado arquitetural esperado (106) | React → API → PostgreSQL operacional; Firebird → Worker → PostgreSQL ainda pendente. | Concluir e homologar a integração antes de declarar a fase 2 entregue. |

## Registro de verificacoes

| Data | Escopo | Comando ou evidencia | Resultado |
| --- | --- | --- | --- |
| 2026-09-02 | Static Web App dedicado | `az staticwebapp create --name orobi-web --resource-group rg-oroleite-site --location eastus2 --sku Free` | Sucesso: `orobi-web` criado em East US 2 com hostname `lively-sea-0776c9a0f.6.azurestaticapps.net`; `orlt-bi` nao foi modificado. |
| 2026-09-02 | Deploy, health e migracao Azure | `scripts/deploy-azure.ps1 -Apply -ConfigureRuntimeSecrets -ApiImage orobiacr.azurecr.io/orobi-api:20260901.2 -WebOrigin https://orange-island-06ceb30f.7.azurestaticapps.net`; consulta do Container App; `GET /health`; `az containerapp job start` e consulta da execucao | Sucesso: revisao `orobi-api--0000007` em `Succeeded`/`Running`, health `200 {"status":"healthy"}` e job `orobi-migrate-sgt082o` em `Succeeded` apos 31 segundos. |
| 2026-09-02 | Protecao do script de deploy | `Invoke-Pester tests/Operations/DeployAzure.Tests.ps1`; execucao de `deploy-azure.ps1 -Apply -WhatIf` sem parametros; suite operacional e Bicep | Sucesso: o guard bloqueia configuracao incompleta antes de consultar Azure; 14 testes Pester passaram e o Bicep compilou. |
| 2026-09-02 | Auditoria e verificacao local | `Invoke-Pester` para bootstrap, deploy, Key Vault e workflows; `az.cmd bicep build --file infra/main.bicep` com `AZURE_CONFIG_DIR` local; `dotnet test OroBI.slnx --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false`; testes e build em `src/OroBI.Web` | Sucesso: 8 + 1 + 2 + 2 testes Pester, Bicep compilado, 48 testes .NET, 7 testes Web e build Web sem falhas. O workflow Azure nao envia mais parametro ausente do template. |
| 2026-08-31 | Segredos Azure | Pester para Bicep, bootstrap e deploy; `az.cmd bicep build --file infra/main.bicep` | Sucesso: 3 + 2 + 1 testes Pester passaram e o Bicep compilou. Valores reais e deploy Azure permanecem pendentes. |
| 2026-08-31 | Build Web | `npx.cmd vite build --configLoader native` em `src/OroBI.Web` | Sucesso. O loader padrao do Vite falha neste ambiente com `spawn EPERM`. |
| 2026-08-31 | Tipagem Web | `npx.cmd tsc -b` em `src/OroBI.Web` | Sem erros observados na execucao iniciada; registrar novamente com saida final antes de usar como aceite. |
| 2026-08-31 | Testes .NET | `dotnet test` das suites Application, Infrastructure e API Integration | Em andamento no momento da criacao deste arquivo; resultado ainda nao registrado. |
| 2026-08-31 | Testes Web | `npm.cmd --prefix src/OroBI.Web test -- --run` | Sucesso: 2 testes passaram. |
| 2026-08-31 | Build Web | `npm.cmd --prefix src/OroBI.Web run build` | Sucesso: TypeScript e Vite concluiram o bundle de producao. |
| 2026-08-31 | Testes Web | `npm.cmd --prefix src/OroBI.Web test -- --run` | Sucesso: 4 testes passaram, incluindo fechamento e filtro na URL. |
| 2026-08-31 | Build Web | `npm.cmd --prefix src/OroBI.Web run build` | Sucesso: TypeScript e Vite concluiram o bundle de producao apos a tela de fechamento. |
| 2026-08-31 | Testes e build Web | `npm.cmd test -- --run` e `npm.cmd run build` em `src/OroBI.Web` | Sucesso: 5 testes passaram e o bundle de producao foi gerado apos a cobertura de Venda x troca. |
| 2026-08-31 | Testes e build Web | `npm.cmd test -- --run` e `npm.cmd run build` em `src/OroBI.Web` | Sucesso: 7 testes passaram e o bundle de producao foi gerado apos as coberturas de login e importacao. |
| 2026-08-31 | Testes .NET | `dotnet test OroBI.slnx --configuration Release --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Sucesso: 38 testes passaram, sem falhas. |
| 2026-08-31 | Testes .NET Release | Suites API Integration, Application e Infrastructure executadas separadamente com `--no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Sucesso: 6 + 25 + 13 = 44 testes passaram, sem falhas. |
| 2026-08-31 | Bicep | `az.cmd bicep version` e `az.cmd bicep build --file infra/main.bicep` com `AZURE_CONFIG_DIR` local | Bloqueado: Bicep nao esta instalado; o Azure CLI nao consegue acessar `aka.ms` porque o proxy `127.0.0.1:9` recusa a conexao. |
| 2026-08-31 | Bicep | `az.cmd bicep build --file infra/main.bicep` com `AZURE_CONFIG_DIR` local e proxy removido somente do processo | Sucesso: Bicep 0.46.1 compilou o template e gerou `infra/main.json`. |
| 2026-08-31 | Sessao Azure | `az.cmd account show --output json` com `AZURE_CONFIG_DIR` local | Bloqueado: Azure CLI solicitou `az login`; nao ha assinatura autenticada no ambiente. |
| 2026-08-31 | Recursos Azure | `az.cmd resource list --resource-group rg-oroleite-site` | Sucesso: grupo acessivel; contem `orlt-site`, `orlt-bi` (Static Web Apps) e `storltsite` (Storage), sem Container Registry ou Container App. |
| 2026-08-31 | ACR e imagem API | `az deployment group create` para `infra/acr.bicep`; `az acr build` remoto | Sucesso: `orobiacr.azurecr.io` criado como Basic sem usuario administrador; run `ch2` publicou `orobi-api:20260831`. O primeiro run falhou por divergencia entre SDK 10.0.400 e `global.json` 10.0.201; Dockerfile foi alinhado. |
| 2026-08-31 | Teste pre-deploy | `Invoke-Pester tests/Operations/DeployAzure.Tests.ps1` com politica `Bypass` apenas no processo | Sucesso: 1 teste passou; o script falha de forma explicita quando os segredos nao foram definidos. |
| 2026-08-31 | Key Vault | `az deployment group create` para `infra/key-vault.bicep`; tentativa de `az role assignment create` | Key Vault criado com RBAC e purge protection. Bloqueado: a conta autenticada nao possui `Microsoft.Authorization/roleAssignments/write` no escopo do cofre. |
| 2026-08-31 | API v1 | `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Sucesso: 8 testes passaram, incluindo login e dashboard em `/api/v1`. |
| 2026-08-31 | Verificacao final | `dotnet test OroBI.slnx --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false`; testes e build em `src/OroBI.Web` | Sucesso: 46 testes .NET, 7 testes Web e build de producao Web passaram. |
| 2026-08-31 | Persistencia Firebird | `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Sucesso: 13 testes passaram; migration `AddSynchronizationAudit` foi gerada. |

## Regra de atualizacao

Ao iniciar ou concluir uma alteracao:

1. Atualize a tabela da fase correspondente com status e proxima acao.
2. Adicione uma linha em `Ultima alteracao` com data, item e impacto.
3. Registre no `Registro de verificacoes` o comando executado e seu resultado real.
4. Se um requisito novo vier de um PDF, indique a fase e o documento antes de implementa-lo.
