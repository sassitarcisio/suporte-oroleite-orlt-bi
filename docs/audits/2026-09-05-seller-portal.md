# Entrega local — Portal do Vendedor

Em 2026-09-05, o MVP da fase 3 foi implementado na branch `feature/seller-portal-phase-3`, preservando alterações anteriores do diretório. A primeira entrega foi local; na continuação, o usuário autorizou prosseguir e esclareceu que os vendedores devem se cadastrar. O registro de ativação abaixo acompanha essa segunda etapa.

## Escopo implementado

- Identidade persistida por vendedor, permissões, gestores com vínculos limitados, administração de contas e sessões revogáveis com auditoria.
- Dashboard, vendas paginadas, clientes e detalhes, produtos, marcas, metas, PPP, trocas e comissão, com autorização aplicada no backend.
- Conferência e aprovação administrativa de fechamento, snapshot imutável, histórico e consulta individual administrativa usando o mesmo resultado aprovado.
- Interface responsiva em `/portal`, perfil, gestão de acessos, manifest, ícones e fallback offline sem cache de APIs autenticadas.
- Autocadastro público com nome/e-mail/senha, conta pendente sem privilégios e aprovação administrativa por vendedor/permissões. O provisionamento inicial não promove automaticamente contas originadas no cadastro público.

As revisões independentes identificaram e motivaram correções cobertas por regressão: agregados especiais fora do escopo pessoal, contagem de clientes sem permissão, aliases duplicando identidade financeira, mensagens de erros administrativos, logout durante falha de identidade e rótulos de resultados aprovados.

## Evidências finais

| Verificação | Resultado |
| --- | --- |
| .NET Release, incluindo autocadastro | 316 aprovados: 127 API, 77 Application e 112 Infrastructure |
| Vitest, quatro workers | 101 aprovados em 19 arquivos |
| TypeScript e Vite | Build de produção aprovado |
| Oxlint | Sem erros; três avisos em App.tsx, dois de preserve-manual-memoization e um de set-state-in-effect |
| Chrome local | 50 verificações aprovadas nas larguras 360, 390, 430, 768 e 1440; navegação, logout, cache, ícones e fallback offline |
| EF Core | Migration e SQL idempotente gerados; nenhuma alteração pendente no modelo |

Os comandos de reprodução estão no [guia operacional](../operations/seller-portal.md). O [relatório do navegador](evidence/portal-ui-verification.json) registra os resultados e dez capturas de tela. O teste usa APIs fictícias locais e bloqueia requisições externas; as capturas usam fonte local de fallback.

## Limites e ativação

Nenhuma migration foi executada em PostgreSQL real. Testes de persistência usam EF InMemory; transações, índices e comportamento relacional precisam de homologação no ambiente de destino. O [SQL gerado](../operations/2026-09-05-seller-portal.sql) está disponível para revisão.

A publicação exige aplicar a migration, publicar API e SPA compatíveis e cadastrar contas/vínculos reais. Tokens antigos deixam de ser aceitos; usuários devem entrar novamente. A emulação de navegador não certifica instalação em Android/iPhone físicos nem a configuração publicada do Azure.

A fonte atual continua CSV. Firebird automático e carteira completa dependem da fase 2. Calendário/projeções, análise de queda, notificações/push, ranking configurável, reabertura de fechamento e aplicativo nativo permanecem fora deste MVP. A folha consolidada legada mantém cálculo operacional; a aprovação oficial é individual por snapshot.
