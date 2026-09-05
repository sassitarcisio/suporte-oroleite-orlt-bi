# Correção dos achados da auditoria

> **For agentic workers:** Use superpowers:subagent-driven-development to implement the independent tasks, with review before publication.

**Goal:** Corrigir A01–A09 da auditoria sem alterar as regras financeiras existentes.

**Architecture:** Aplicar autorização e bloqueio no servidor; normalizar parsing e identidade dos vendedores; aplicar filtros antes de materializar movimentos; separar filtros em edição dos aplicados e tratar expiração de sessão na Web. Manter contratos públicos compatíveis e validar os valores oficiais.

**Tech Stack:** ASP.NET Core/.NET 10, Identity, EF Core/Npgsql, React/TypeScript, Azure Container Apps/Static Web Apps.

**Spec:** `docs/audits/2026-09-05-system-audit.md`, achados A01–A09. Usuário autorizou a correção e a publicação; a autorização de envio do código ao registro orobiacr permanece válida.

**Restrições:** Não alterar dados de produção, regras de PPP/prêmios/salários nem fórmulas para atingir valores fixos. Usar dados sintéticos nos testes de segurança/importação. Não expor segredos. Não incluir bin/obj no versionamento. Não executar o deployment de infraestrutura para testar a falha A02.

**Tarefa 1 — Segurança e implantação (A01–A03)**

Arquivos: `Program.cs`, `ClosingEndpoints.cs`, `LocalAuthenticationService.cs`, políticas/autenticação, `.github/workflows/deploy-azure.yml` e testes correspondentes.

- [x] Testar vendedor próprio/outro/sem claim/aliases, gestor e administrador em `/api` e `/api/v1`.
- [x] Restringir fechamento à identidade do vendedor; acesso amplo apenas gestor/administrador.
- [x] Testar e implementar bloqueio Identity, contagem/reset de falhas e proteção de frequência de login sem vazar identidade.
- [x] Tornar parâmetros essenciais do workflow explícitos/validados e usar passagem segura via environment/script.
- [x] Executar testes específicos e validação Bicep/workflow; revisar alterações.

**Tarefa 2 — Importação e consultas (A04–A06, A09)**

Arquivos: `ImportCsvService.cs`, `CsvImportWorkflow.cs`, `CommercialFilters.cs`, `SellerAliasCatalog.cs`, `DashboardQueryService.cs` e testes de aplicação/infraestrutura.

- [x] Reproduzir em testes CSV curto e BOM em todos os formatos afetados.
- [x] Normalizar cabeçalho compartilhado e registrar linhas truncadas como erros de importação, preservando lote e linhas válidas.
- [x] Aceitar aliases/nome canônico dos dois lados sem perder os dados existentes.
- [x] Aplicar período/vendedor/marca/grupo/cidade/tipo/pesquisas no IQueryable antes de ToListAsync, mantendo deduplicação.
- [x] Testar semântica equivalente, datas inclusivas, pesquisa sem distinção de caixa, lotes repetidos e valores financeiros; confirmar tradução PostgreSQL.
- [x] Avaliado: filtro SQL antes da materialização aplicado; cache/endpoint agregado não necessário nesta correção.
- [x] Executar testes específicos e revisar alterações.

**Tarefa 3 — Interface e sessão (A07–A08)**

Arquivos: `App.tsx`, `api/client.ts`, `auth/session.ts`, `DashboardPage.tsx` e testes Web.

- [x] Testar edição sem aplicar: resumo e outras páginas continuam no último recorte aplicado.
- [x] Separar rascunho e filtro aplicado; aplicar/limpar atualizam ambos explicitamente.
- [x] Testar HTTP 401, logout/login e respostas antigas pendentes.
- [x] Invalidar sessão atual em 401, limpar dados/permissões, mostrar mensagem de expiração e impedir resposta antiga de sobrescrever nova sessão.
- [x] Tratar também exportação/importação; preservar mensagens de erro de rede e 403.
- [x] Executar testes Web/build e conferir navegador.

**Tarefa 4 — Integração, revisão e publicação**

- [ ] Revisão independente das mudanças e correção dos achados dessa revisão.
- [ ] Executar suite .NET e Web, build, lint e validação de infraestrutura.
- [ ] Validar cálculos da base de agosto e autorização/CSV somente em testes locais.
- [ ] Registrar commits apenas dos arquivos necessários; gerar imagem versionada no orobiacr.
- [ ] Publicar API, confirmar saúde e consultas de leitura; publicar Web e verificar filtros/sessão/responsividade.
- [ ] Atualizar auditoria com situação final, testes e limitações verificadas.
