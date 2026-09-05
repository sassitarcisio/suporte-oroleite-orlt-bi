# Seller Portal Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for integration and superpowers:dispatching-parallel-agents for independent components. Steps use checkbox tracking.

**Goal:** Entregar localmente o MVP seguro do Portal do Vendedor descrito na fase 3.
**Architecture:** Mesma API, Identity, PostgreSQL e serviços de cálculo. Escopo por SellerId no backend, DTOs pessoais, snapshots de fechamento e frontend `/portal` PWA.
**Tech Stack:** .NET 10, EF Core/PostgreSQL, React/TypeScript/Vite, xUnit/Vitest.
**Spec:** `docs/superpowers/specs/2026-09-05-seller-portal-design.md`

## Global Constraints

- Publicação autorizada na continuação após a entrega local. Não ativar Firebird, alterar regras comerciais ou criar credenciais reais de funcionários automaticamente.
- Preservar alterações preexistentes; branch local `feature/seller-portal-phase-3`.
- Não retornar custo, margem, salário ou premiação individual de colegas no portal.
- Testes de isolamento antes de criar telas; sem cache de API autenticada.
- Contratos de integração entre agentes registrados neste plano e em mensagens.

## Task 1 — Identidade, sessões e escopo

Files: Domain/Sellers; Application/Identity; Infrastructure/Identity; Api/Auth; DbContext e políticas existentes. Criar `IDataAccessScope.ResolveAsync(ClaimsPrincipal, Guid?, CancellationToken) : Task<SellerAccess?>` e `SellerAccess(Guid SellerId, string Name, string ImportedName, SellerPortalPermissions Permissions)`.

- [x] Testes de identidade vinculada, gestor limitado e revogação, observar falha.
- [x] Implementar entidades, escopo central, autorização de rotas antigas e gestão administrativa de acesso.
- [x] Implementar versão de sessão, logout, troca/reset de senha, desativação e log.
- [x] Executar testes Infrastructure/Identity e integração de autenticação/escopo.

## Task 2 — Consultas pessoais

Files: Application/Portal, Infrastructure/Portal/PortalQueryService.cs, tests/Infrastructure/Portal. Serviço recebe nome importado autorizado; não acessa HttpContext. DTOs sem custos ou dados de colegas. Root integra as rotas e DI após contrato disponível.

- [x] Testes de dois vendedores, faixas 80/90/100, filtros e paginação, observar falha.
- [x] Implementar dashboard, vendas, clientes/detalhes, produtos, marcas, metas, PPP e trocas.
- [x] Reutilizar serviços/calculadoras; expor fonte e última importação/sincronização confirmada.
- [x] Executar testes de aplicação/infraestrutura relevantes.

## Task 3 — Fechamento aprovado

Files: Domain/Closings/ClosingSnapshot.cs, Application/Portal/PortalClosing.cs, Infrastructure/Portal/PortalClosingService.cs, Api/Portal/PortalClosingEndpoints.cs, tests/Infrastructure/Portal/PortalClosingTests.cs.

- [x] Testar transições, imutabilidade após reimportação e projeção segura antes de implementar.
- [x] Persistir snapshot completo no servidor, projeção pessoal segura e histórico.
- [x] Rotas pessoais e gerenciais com escopo e aprovação apenas administrativa.
- [x] Verificar mesmo snapshot em consulta pessoal e administrativa.

## Task 4 — Integração API e migração

- [x] Endpoints `/api/v1/me/*` usando escopo central; rejeitar seleção arbitrária de vendedor.
- [x] Autorizar campos compostos por permissões; no-store, paginação e datas válidas.
- [x] Gerar migration única com entidades novas e garantir compatibilidade de importações.
- [x] Testes API com autenticação real e banco controlado: isolamento, permissões, gestores e sessões.

## Task 5 — Portal e PWA

Files: Web/src/features/portal, Web/src/App.tsx, Web/src/main.tsx, Web/public/manifest.webmanifest e service-worker.js, testes Web.

- [x] Após isolamento aprovado, testes de fluxos pessoais, falhas e logout.
- [x] Criar navegação mobile, telas e administração de acesso/fechamento.
- [x] Manifest, ícones, fallback offline; não cachear API.
- [x] Testes Web, typecheck, build e inspeção de apresentação.

## Task 6 — Revisão e entrega

- [x] Revisão independente de segurança e contratos.
- [x] Corrigir achados; executar suites .NET/Web, build e verificações de migração.
- [x] Atualizar TODO e operação com limites e comandos reais executados.
- [x] Entregar código local e instruções de ativação, sem publicação automática.

## Execution ledger

- Continuação autorizada em 05/09: preparar publicação no ambiente existente e adicionar autocadastro solicitado pelo usuário. Implementação adicional cobre conta pendente sem acesso e aprovação administrativa com vínculo individual. A ativação será registrada com as execuções reais do Azure/GitHub.

- Entrega local concluída em 2026-09-05. Revisões independentes de backend e frontend concluídas; corrigidos vazamentos de agregados especiais, contagem de clientes sem permissão, duplicação por alias, apresentação de erros e saída da conta em falha de identidade.
- Verificação final: 300 testes .NET (111 API, 77 Application, 112 Infrastructure), 94 testes Web e 50 verificações em Chrome local passaram. Build TypeScript/Vite aprovado; lint sem erros e com três avisos em App.tsx.
- Migration e SQL idempotente gerados; EF confirmou ausência de alterações pendentes no modelo. Nenhuma migration aplicada a PostgreSQL real, nenhuma publicação ou criação de conta real.
- Evidências e limites: `docs/audits/2026-09-05-seller-portal.md`. Ativação: `docs/operations/seller-portal.md`.

- 2026-09-05: usuário autorizou a implementação após análise e sequência proposta.
- Branch criada preservando o diretório atual; Git exigiu acesso ampliado somente para criar referência local. Alterações anteriores inventariadas.
- Desenho adota snapshots imutáveis e aprovação administrativa; reabertura, calendário de projeção e carteira ERP ficam explicitamente fora do MVP até definição dos dados/regras.
