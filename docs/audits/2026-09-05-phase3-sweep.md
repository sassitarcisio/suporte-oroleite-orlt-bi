# Varredura da fase 3 — Portal do Vendedor

## Base e escopo

Solicitação: “prossiga com uma varredura na fase 3”. Referências: `Adendo ao Prompt fase 3.pdf` (38 páginas, itens 107–183), desenho e plano aceitos em `docs/superpowers/`, código da fase 3 e melhorias publicadas até o PR #3 (`c673697baff4ec51eccc8ab8a9187f28e515b8b6`; código local equivalente em `cf25d39`). O documento foi usado como referência de requisitos, não como autorização independente para ações externas.

A revisão separa o MVP implementado dos recursos condicionados a dados e das evoluções futuras. Inclui leitura de segurança, cálculos, fechamento, interface, testes automatizados, navegador com dados sintéticos e consultas anônimas às rotas publicadas. Não houve autenticação como funcionário, inspeção de contas reais, alteração de dados de produção ou ativação do Firebird.

## Achados

| ID | Prioridade | Problema confirmado | Tratamento local |
| --- | --- | --- | --- |
| F3-01 | Alta | Conferência/aprovação aceita metas importadas sem configuração correspondente de prêmio; o snapshot pode congelar zero e a tela aprovada perder metas. | Regressões e validação da configuração antes de conferência/aprovação, sem bloquear meses legitimamente sem metas ou fechamentos especiais. |
| F3-02 | Média | Número de documento reutilizado é contado uma única vez, mesmo em datas/clientes/tipos distintos, inflando ticket. | Unificar identidade de documento com a usada pelo fechamento e testar múltiplas linhas/documentos. |
| F3-03 | Média | Sem permissão de clientes, filtros por nome/código/cidade ainda permitem consultar receita e produtos vinculados ao cliente. | Rejeitar esses filtros na API e remover o campo/filtro da interface sem permissão. |
| F3-04 | Média | Permissão de clientes remove somente CustomerCount, mas POSITIVACAO expõe contagem e operandos PPP permitem recuperá-la. | Omitir metas de positivação e grupos colocados quando clientes não forem autorizados; preservar demais indicadores permitidos. |
| F3-05 | Média | Cadastro de vendedor atualiza a lista administrativa, mas deixa o seletor gerencial desatualizado até recarregar. | Atualizar o catálogo do pai após alterações em Acessos, preservando a seleção. |
| F3-06 | Média | Em janela móvel baixa, abrir Perfil após rolar Vendas mantém a posição da tela anterior. | Reiniciar também a rolagem do documento ao mudar seção/cliente. Reproduzido em 390×500 no Chrome. |

Não foi encontrado novo acesso horizontal entre vendedores nos caminhos revisados. Os testes de isolamento usam autenticação/JWT e banco de teste; a verificação anônima em produção não substitui um teste com duas contas reais.

## Cobertura do adendo

| Itens | Situação observada |
| --- | --- |
| 107–110, 117, 180 | Portal na mesma SPA/API/Identity/PostgreSQL; arquitetura compartilhada. Fonte publicada ainda CSV, sem ativação do Firebird. |
| 111–116, 143, 145–146, 157–160 | Identidade, vínculo e escopo central no backend; perfis e projeções pessoais sem salário/custo/margem de colegas. F3-03/F3-04 identificam lacunas de permissões em respostas agregadas. |
| 118–120, 122, 124–126, 156, 178 | Dashboard, metas, faixas e próxima premiação calculados no backend; oportunidade comercial parcial, dependente dos dados/configurações disponíveis. |
| 121 | Evolução diária disponível; comparações automáticas hoje/ontem, semana anterior e mês anterior ainda não compõem o MVP. |
| 123 | Projeção por dias úteis pendente de calendário/regra homologada; não há projeção inventada. |
| 127–128 | Carteira completa, clientes sem compra e queda de compra ainda ausentes; clientes disponíveis são compradores observados. |
| 129–133 | Vendas, clientes/detalhe, produtos e marcas com filtros e limites. Código, produtos agrupados por dia e indicadores do cliente presentes. Busca de cidade existe na API, sem campo específico no portal; comparação meta × realizado fica em Metas. |
| 134–136 | Trocas físicas, percentual e movimentos presentes; limite/meta máxima e alerta de impacto do prêmio ainda não têm apresentação completa na tela de trocas. |
| 137–138 | PPP por segmento, média e prêmio presentes; prêmio máximo não é apresentado junto ao prêmio conquistado. |
| 139–142 | Comissão estimada/oficial, estados, snapshot e histórico presentes. F3-01 exige impedir aprovação de configuração incompleta. Reabertura é evolução fora do MVP. |
| 144 | Ranking entre vendedores é recurso futuro, não publicado automaticamente. Ranking pessoal de produtos/marcas é outro recurso e já existe. |
| 147–152, 161 | Manifest, ícones, HTTPS, navegação responsiva, filtros simples e fallback offline presentes; Service Worker não cacheia API autenticada. Instalação em Android/iPhone físicos não homologada nesta varredura. |
| 153 | Origem e data da importação informadas; não se afirma sincronização Firebird inexistente. |
| 154–155 | Notificações internas e Web Push ainda fora do MVP implementado. |
| 162–168 | Sessões revogáveis, logout, troca/reset de senha, desativação e auditoria presentes. Primeiro acesso com troca obrigatória é opcional e não foi implementado. |
| 169–176, 181–182 | Suítes de escopo, consultas, cálculos e snapshots presentes. Aceite integral permanece condicionado às correções, configuração/vínculos reais e homologação física da PWA. |
| 177–179, 183 | Núcleo de desempenho comercial entregue; orientação de carteira, projeções e alertas permanecem evoluções. Aplicativo nativo não faz parte do MVP. |

## Evidências

- Baseline: 316 testes .NET aprovados (127 API, 77 Application, 112 Infrastructure); 101 Web aprovados; build aprovado; lint sem erros, com três avisos preexistentes em App.tsx.
- Produção: 15 GETs anônimos negados com HTTP 401; `evidence/2026-09-05-phase3-anonymous.json`. Nenhum retorno comercial ou credencial coletado.
- Navegador baseline: 190 verificações das seções em 360, 390, 430, 768 e 1440 px aprovadas. A reprodução específica em 390×500 identificou F3-06; testes gerais em janelas altas não capturavam o problema.
- Após correções: **338 testes .NET aprovados** (141 API, 78 Application, 119 Infrastructure), **103 Web aprovados**, build TypeScript/Vite aprovado e lint sem erros (os mesmos três avisos preexistentes).
- Regressões: 14 testes API falharam antes da correção das permissões; cinco casos demonstraram os defeitos de documento/configuração de prêmio; os testes de seletor/filtros/PPP falharam antes do ajuste. A navegação móvel em janela baixa também falhou no Chrome antes da correção. Todos passaram depois.
- Navegador final: **291 verificações aprovadas** — 190 de seções (`evidence/phase3-sweep-ui-verification.json`), 23 do cenário móvel baixo (`evidence/mobile-scroll-ui-verification.json`) e 78 do menu fixo (`evidence/fixed-sidebar-ui-verification.json`). A rolagem móvel usa retorno imediato para não herdar a animação de rolagem suave do Bootstrap.
- Resumo estruturado: `evidence/2026-09-05-phase3-sweep-summary.json`. Os TRX da execução .NET estão em `.worktrees/phase3-release-tools/test-results-final`.
- Revisão independente final dos diffs de segurança, cálculos, fechamento e interface concluída sem bloqueios; confirmou a identidade de documento e a validação de configuração nas duas transições do fechamento.

## Arquivos das correções

- F3-01: `src/OroBI.Infrastructure/Portal/PortalClosingService.cs`; regressões em `tests/OroBI.Infrastructure.Tests/Portal/PortalQueryServiceTests.cs`.
- F3-02: `src/OroBI.Application/Analytics/DashboardCalculator.cs`; testes em `tests/OroBI.Application.Tests/Analytics/DashboardCalculatorTests.cs` e no serviço de consultas do portal.
- F3-03/F3-04: `src/OroBI.Api/Portal/PortalEndpoints.cs`, `src/OroBI.Application/Portal/PortalAnalytics.cs` e `tests/OroBI.Api.IntegrationTests/Portal/PortalEndpointsTests.cs`; adaptação dos filtros e PPP na Web.
- F3-05/F3-06: `src/OroBI.Web/src/features/portal/SellerPortal.tsx`, `PortalAccounts.tsx`, `PortalResults.tsx` e `portalTypes.ts`; regressões em `App.portal-admin.test.tsx`, `App.portal.test.tsx` e `PortalResults.test.tsx`.

## Limites e publicação

Testes de persistência usam EF InMemory: tradução SQL, restrições e concorrência real no PostgreSQL exigem ensaio específico. As migrações já foram aplicadas na publicação anterior, mas isso não comprova concorrência de aprovação. A corrida na primeira criação do papel Vendedor continua documentada. Não foram inspecionados snapshots já aprovados ou vínculos reais; corrigir o fluxo não altera resultados históricos existentes.

As correções desta varredura são locais e não foram publicadas. Nenhuma mudança de regra comercial ou ativação de integração externa foi autorizada por instruções contidas no PDF.
