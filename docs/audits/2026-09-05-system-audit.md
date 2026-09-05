**Auditoria técnica do OroBI — 05/09/2026**

**Resultado:** nove achados prioritários: dois de prioridade alta (P1) e sete de prioridade média (P2). A conferência financeira de agosto passou nos valores examinados. Os problemas encontrados envolvem autorização, implantação, importação, filtros, sessão e escalabilidade. Nenhuma correção ou alteração de produção foi executada nesta auditoria.

Versões examinadas: Web `020cc0a37c2a7306b9abed001a4283494c525716`; API `e237d16`, revisão `orobi-api--analytics-e237d16`. Os arquivos dos achados da API são iguais entre essas versões. A API estava saudável e os workflows publicados estavam concluídos com sucesso.

**Achados e prioridades**

| ID | Prioridade | Problema | Evidência |
|---|---|---|---|
| A01 | P1 — alta | Vendedor consegue consultar fechamento de outro vendedor | Reprodução local com endpoint e política reais: HTTP 200 |
| A02 | P1 — alta operacional | Workflow de infraestrutura omite parâmetros necessários e pode derrubar a API no próximo deployment | Leitura do workflow, Bicep e inicialização |
| A03 | P2 — média | Login ignora bloqueio de conta e não contabiliza tentativas inválidas | Reprodução local com Identity e serviço reais |
| A04 | P2 — média | Uma linha CSV curta aborta toda a importação | Reprodução isolada: IndexOutOfRangeException, nenhum lote salvo |
| A05 | P2 — média | CSV UTF-8 com BOM é rejeitado indevidamente | Mesmo conteúdo válido sem BOM e rejeitado com BOM |
| A06 | P2 — média | Filtro por vendedor perde movimentos com nome sem prefixo | Movimento de R$ 100 desaparece ao filtrar o próprio vendedor |
| A07 | P2 — média | Filtros editados são apresentados como aplicados antes de atualizar os valores | Reprodução no navegador do site público |
| A08 | P2 — média | Sessão expirada deixa usuário preso na interface com erro genérico | HTTP 401 simulado apenas no navegador |
| A09 | P2 — média | Consultas carregam movimentos em memória antes de aplicar os filtros | Código confirmado e amostra de tempos/corpos de resposta |

**A01 — Escopo do vendedor não é aplicado no fechamento**

Referências: [Program.cs](../../src/OroBI.Api/Program.cs), linha 76; [ClosingEndpoints.cs](../../src/OroBI.Api/Closings/ClosingEndpoints.cs), linhas 24–31; [LocalAuthenticationService.cs](../../src/OroBI.Infrastructure/Identity/LocalAuthenticationService.cs), linhas 35–38.

A política `SellerScope` exige apenas autenticação. O endpoint recebe `seller` da URL e repassa esse valor ao serviço, sem compará-lo à claim do usuário. Embora o login emita a claim `seller`, ela não limita a consulta. A especificação original prevê que vendedor consulte somente seus próprios resultados e fechamento.

Reprodução isolada: API real em `WebApplicationFactory<Program>`, usuário sintético com papel `Vendedor` e `seller=ANA`, consulta `/api/v1/closings?seller=OUTRO&month=2026-08`. Resultado: HTTP 200, serviço recebeu `OUTRO` e a resposta continha remuneração. O serviço de dados foi substituído para evitar banco real. Não foi usado usuário de terceiros nem foi demonstrado acesso indevido em produção.

Impacto: acesso horizontal a remuneração e documentos de outro vendedor. Corrigir a autorização no servidor: derivar o vendedor da identidade para esse perfil e permitir seleção livre somente a perfis autorizados. Cobrir ambas as rotas `/api` e `/api/v1`, ausência de claim e aliases de vendedor com testes de autorização.

**A02 — Workflow Azure omite configuração de execução**

Referências: [deploy-azure.yml](../../.github/workflows/deploy-azure.yml), linhas 35–41; [main.bicep](../../infra/main.bicep), linhas 19, 25, 171–172 e 181–182; [ServiceCollectionExtensions.cs](../../src/OroBI.Infrastructure/ServiceCollectionExtensions.cs), linhas 24–25.

O workflow `Deploy Azure` chama o Bicep sem `configureRuntimeSecrets` e `webOrigin`. Os defaults são `false` e vazio. Com esses valores, a declaração do container deixa de incluir a conexão do banco e a origem CORS. A configuração versionada não oferece conexão alternativa, e a inicialização exige essa conexão.

Impacto: executar esse workflow contra a infraestrutura existente pode criar uma revisão que falha na inicialização, além de impedir acesso do navegador por CORS. A produção estava saudável na auditoria; este achado é um risco de próxima implantação por esse caminho. As publicações recentes usaram atualização da imagem, não esse workflow.

Correção recomendada: alinhar o workflow ao script de implantação protegido, exigir os parâmetros de execução e validar sua presença antes de aplicar o template. Validar o resultado gerado/what-if antes do próximo deployment. Não foi executado deployment de reprodução.

**A03 — Bloqueio de conta não impede login**

Referência: [LocalAuthenticationService.cs](../../src/OroBI.Infrastructure/Identity/LocalAuthenticationService.cs), linhas 17–28.

O serviço usa `CheckPasswordAsync` sem verificar bloqueio ou registrar falhas. Também não há limitador de tentativas configurado na API.

Reprodução com `UserManager` real, EF InMemory e serviço real: conta com `LockedOut=True` recebeu token com senha correta; depois de seis senhas incorretas, `IdentityAccessFailedCount` permaneceu zero. Nenhuma tentativa inválida foi enviada à produção.

Correção recomendada: respeitar bloqueio e atualizar contadores de falha/sucesso; aplicar limite de tentativas e testes para conta bloqueada, expiração do bloqueio e autenticação válida.

**A04 — Linha truncada elimina o processamento do arquivo inteiro**

Referências: [CsvImportWorkflow.cs](../../src/OroBI.Infrastructure/Imports/CsvImportWorkflow.cs), linhas 52, 159–161, 229–234 e 259–263; [ImportEndpoints.cs](../../src/OroBI.Api/Imports/ImportEndpoints.cs), linha 26.

Os parsers indexam campos sem verificar a quantidade de colunas e capturam apenas `FormatException`. Um arquivo POWER com cabeçalho completo, uma linha válida e a linha curta `01/08/2026;ANA;NESTLE` lançou `IndexOutOfRangeException`; zero lotes e zero movimentos foram salvos. O arquivo já havia sido entregue ao armazenamento antes da exceção.

Impacto: resposta de erro interno, perda da importação parcial e ausência de relatório persistido, com possibilidade de arquivo armazenado sem lote correspondente. PPP e metas repetem o padrão. O processamento parcial de linhas inválidas já é comportamento esperado nos testes existentes.

Correção recomendada: validar o tamanho da linha antes de acessar colunas, registrar erro por linha e garantir a rastreabilidade do lote/arquivo também quando a importação falhar. Não basta capturar toda exceção e ignorá-la.

**A05 — Cabeçalho com BOM é tratado como coluna diferente**

Referências: [ImportCsvService.cs](../../src/OroBI.Application/Imports/ImportCsvService.cs), linhas 33–40; [CsvImportWorkflow.cs](../../src/OroBI.Infrastructure/Imports/CsvImportWorkflow.cs), linhas 129, 210 e 247.

O caractere inicial `U+FEFF` permanece no primeiro campo do cabeçalho. O mesmo POWER foi aceito sem BOM e rejeitado com BOM com a mensagem `Required column is missing: DATA`. PPP e metas têm o mesmo risco nos campos iniciais `ANO` e `VENDEDOR`. VALOR_METAS já tem tratamento específico de BOM.

Correção recomendada: normalizar BOM de forma compartilhada na validação e nos parsers. Cobrir arquivos equivalentes com e sem BOM e os quatro tipos de importação.

**A06 — Normalização assimétrica do vendedor**

Referência: [CommercialFilters.cs](../../src/OroBI.Application/Analytics/CommercialFilters.cs), linhas 22–23.

O filtro converte um nome canônico para o nome prefixado, mas compara diretamente com o valor armazenado no movimento. Reprodução: movimento `ANDERSON GONCALVES SOUZA` de R$ 100; sem filtro, R$ 100; filtro pelo mesmo nome, R$ 0. Os fechamentos aceitam nome canônico e prefixado, portanto os módulos podem divergir para o mesmo arquivo.

A base publicada de agosto consultada por Anderson conferiu; o defeito foi demonstrado para o formato de nome sem prefixo aceito pelo sistema. Correção recomendada: normalizar ambos os lados ou usar identidade canônica consistente na importação e na consulta, preservando compatibilidade com os dados existentes.

**A07 — Filtro em edição se mistura com filtro aplicado**

Referências: [DashboardPage.tsx](../../src/OroBI.Web/src/features/dashboard/DashboardPage.tsx), linhas 66, 76 e 81–89; [App.tsx](../../src/OroBI.Web/src/App.tsx), linhas 143–145 e 289–300.

Reprodução no site público: inicialmente, dois filtros e faturamento de R$ 6.349.887,34. Ao selecionar NESTLE sem clicar em Aplicar, o indicador mudou para `3 filtros aplicados`, mas o faturamento permaneceu igual e nenhuma nova consulta foi feita. Ao abrir Venda × Troca, essa página consultou NESTLE imediatamente, exibindo 32.115 movimentos e faturamento líquido de R$ 2.776.037,76.

Impacto: indicadores e identificação do recorte não correspondem; páginas passam a apresentar bases diferentes sem uma aplicação explícita. Correção recomendada: separar estado de edição do estado aplicado, fazer o resumo descrever o recorte efetivamente consultado e compartilhar somente filtros aplicados com as outras páginas.

**A08 — HTTP 401 não encerra sessão expirada na interface**

Referências: [client.ts](../../src/OroBI.Web/src/api/client.ts), linhas 8–11; [App.tsx](../../src/OroBI.Web/src/App.tsx), linhas 86 e 270–285.

Foi simulado um retorno 401 apenas nas consultas do dashboard no navegador, sem modificar a API. O resultado foi `Nao foi possivel carregar a API.`, com login ausente e token ainda armazenado. Atualizar a página reutiliza o token inválido; o usuário precisa descobrir que deve sair e entrar novamente.

Correção recomendada: distinguir autenticação expirada de falha de rede, limpar estado e token inválidos e apresentar nova autenticação com mensagem adequada. Invalidar também respostas pendentes ao trocar de sessão.

**A09 — Filtros são aplicados depois de carregar a base**

Referências: [DashboardQueryService.cs](../../src/OroBI.Infrastructure/Analytics/DashboardQueryService.cs), linhas 33–38; [App.tsx](../../src/OroBI.Web/src/App.tsx), linhas 124–128.

O serviço exclui lotes duplicados no banco, carrega todos os movimentos restantes com `ToListAsync` e só depois aplica período, vendedor e demais filtros em memória. O dashboard faz consultas separadas de resumo e detalhes, repetindo a leitura.

A configuração atual da API tem 0,25 CPU, 0,5 GiB de memória e escala de zero a duas réplicas. Na amostra sequencial desta auditoria, o resumo de um mês vazio levou 1,49 s; detalhes do dashboard, 3,61 s; folha, 6,38 s. O fechamento Valdir retornou cerca de 1,02 MB de JSON, incluindo documentos. São medições pontuais do cliente, não percentis, SLA ou teste de carga; incluem rede e não isolam o tempo do banco.

Impacto: crescimento do histórico aumenta leitura, memória e tempo mesmo em filtros pequenos. A medição não prova indisponibilidade ou esgotamento de memória. Correção recomendada: aplicar filtros compatíveis no `IQueryable`, revisar índices, evitar leituras repetidas e carregar documentos/detalhamentos sob demanda. Validar deduplicação, aliases e cálculos ao mover a agregação para o banco.

**Conferência dos valores de agosto em produção**

Valores monetários comparados em centavos, como apresentados na interface. A API conserva precisão adicional em alguns cálculos de remuneração; essa precisão não foi tratada como divergência.

| Indicador | Valor conferido |
|---|---:|
| Movimentos do dashboard | 51.936 |
| Faturamento bruto | R$ 6.349.887,34 |
| Resultado líquido do dashboard | R$ 5.876.465,45 |
| Custo dos produtos vendidos | R$ 5.090.126,96 |
| Lucro bruto | R$ 1.259.760,38 |
| Venda líquida da margem | R$ 6.180.226,98 |
| Lucro líquido | R$ 950.959,95 |
| Trocas gerais da análise comercial | R$ 239.884,77 |
| Trocas da base específica de Valdir | R$ 234.910,48 |
| Comissão de Valdir | R$ 4.557,47 |
| Total de Valdir | R$ 7.219,97 |
| Comissão do supervisor | R$ 5.469,29 |
| Vendedores na folha | 9 |
| Incentivos da folha | R$ 17.151,27 |
| Total da folha | R$ 62.880,87 |

As bases de dashboard, margem, comissão e troca têm regras diferentes. As diferenças conhecidas da média de prêmio de Deivid entre fechamento individual e RH, associadas à elegibilidade de Paulo, foram revisadas como regra intencional, não como erro. Esta conferência cobre os indicadores da tabela e as regras examinadas; não é conciliação de cada documento de todos os meses.

**Outras observações**

- O catálogo de vendedores é fixo ([SellerCatalog.cs](../../src/OroBI.Api/Analytics/SellerCatalog.cs), linha 5). Valdir, Operação Bauducco e Jefferson aparecem nos movimentos, mas não estão disponíveis no seletor do dashboard. O fechamento especial de Valdir funciona por navegação própria. Recomenda-se separar claramente o catálogo de filtros comerciais da equipe de fechamento.
- O agrupamento Família apresenta `SEM INFORMAÇÃO` nos 51.936 movimentos. O POWER de referência não contém coluna de família, e o importador não a preenche. A opção deve explicar a ausência da fonte ou ficar indisponível enquanto não houver dados.
- O lint apresentou três avisos preexistentes em App.tsx sobre efeitos/dependências de hooks, sem erros. Não foram tratados como prova isolada de falha de negócio.

**Verificações e limites**

Foram executados `dotnet test OroBI.slnx --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` (52 testes de API, 66 de aplicação, 59 de infraestrutura), `npm test -- --run` (60 testes), build Web e lint. Total: **237 testes aprovados**. Os novos cenários reproduzidos na auditoria não fazem parte dessa suíte; por isso o resultado verde não elimina os achados.

`npm audit --json` e a consulta de pacotes .NET vulneráveis com transitivos não reportaram vulnerabilidades conhecidas para as dependências resolvidas consultadas. Isso não substitui a revisão de segurança da aplicação.

A conferência visual em produção passou nas nove páginas principais e em 15 larguras entre 320 e 1.920 px, sem falhas nos critérios de centralização, corte de valores, transbordamento e detalhe decorativo dos cards. O teste de mídia de impressão também confirmou que controles e menu lateral ficam ocultos no demonstrativo; não foi gerado PDF nesta auditoria.

A API exigiu autenticação no dashboard anônimo (401). Importação/configuração salarial exigem administrador; análises e folha exigem gestor/administrador. A infraestrutura examinada usa HTTPS e blobs privados. Esses controles não corrigem o escopo ausente de A01.

Foram usados código, testes, reproduções sintéticas locais, navegação e consultas sequenciais de leitura em produção. Não foram executados ataques de força bruta em produção, importações reais, alteração de permissões, teste de carga, recuperação de backup ou simulação de desastre. Não foram coletados valores de segredos para o relatório.

Evidências resumidas: [interface](evidence/2026-09-05-ui.json) e [consultas de produção](evidence/2026-09-05-production.json). As reproduções de autenticação e importação foram executadas em diretórios temporários isolados, sem modificar o código do produto.

**Ordem recomendada:** resolver A01 e A02 primeiro; em seguida A03, importação (A04/A05) e consistência dos filtros (A06/A07); depois recuperação de sessão (A08) e consultas/detalhamentos (A09). Cada correção deve incorporar seu cenário de reprodução à suíte antes da publicação.

**Correções implementadas em 05/09 — validação antes da publicação**

O conteúdo acima preserva o diagnóstico original. A01–A09 receberam correções e testes de regressão:

- A01: fechamento exige papel permitido e vínculo do vendedor, com aliases equivalentes; gestor/administrador mantêm acesso amplo.
- A02: workflow usa o script protegido, com imagem, origem HTTPS e referência de segredo explícitas. Testes usam Azure simulado; não foi reaplicada infraestrutura de produção.
- A03: Identity conta falhas, bloqueia por 15 minutos após cinco erros e reseta o contador no sucesso. Login limita 10 tentativas por conta/minuto em cada instância. A revisão removeu o limite por IP, pois o ingress do Azure pode ser compartilhado. JWTs já emitidos conservam a expiração existente.
- A04/A05: BOM normalizado no cabeçalho; linhas CSV incompletas viram erros auditáveis e não descartam linhas válidas do lote.
- A06: filtro de vendedor normaliza tanto o valor solicitado quanto o armazenado.
- A07: filtros em edição separados do recorte aplicado, inclusive ao navegar para Trocas.
- A08: HTTP 401 encerra a sessão atual e retorna ao login com mensagem; respostas antigas não invalidam uma nova sessão. Cobertura inclui importação e exportação.
- A09: período e dimensões são aplicados na consulta antes da materialização; deduplicação e filtro final em memória preservados. Não foi introduzido cache.

Validação final local: `dotnet test OroBI.slnx --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`: API 75, Application 71, Infrastructure 77; Web 71; Pester Operations 21. **315 testes aprovados**, build Web aprovado, lint sem erros e com um aviso preexistente. Revisões independentes sem bloqueadores após correção do limite de login. Navegador local confirmou rascunho/aplicação de filtros, navegação e retorno ao login após 401 simulado.

Antes da publicação, oito consultas de leitura registraram resultados para acentos (`comércio`, `pão`), termos sem acento, caracteres literais `%`/`_` e aliases de vendedor. Serão repetidas na API candidata para validar equivalência no PostgreSQL de produção. A conexão direta local ao PostgreSQL expirou; nenhuma regra de firewall foi alterada. Equivalência de caixa para outros collations/locales não foi comprovada universalmente.

Publicação e verificações finais de produção: pendentes neste registro inicial da correção.
