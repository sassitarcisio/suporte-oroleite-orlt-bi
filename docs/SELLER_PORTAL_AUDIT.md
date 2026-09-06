# Auditoria do acesso mobile dos vendedores

**Estado da entrega:** implementação local concluída nas áreas abaixo; o inventário preserva o diagnóstico anterior ao código. Publicação, aplicação da migração em PostgreSQL real e homologação em aparelhos físicos não foram realizadas nesta etapa.

Diagnóstico anterior à implementação, em 05/09/2026, sobre `7b9443f`. Referência: pedido do responsável “AUDITAR E COMPLETAR O ACESSO MOBILE DOS VENDEDORES” (68 seções). Reaproveitar a aplicação publicada, inclusive o autocadastro sujeito à aprovação já solicitado. Na decisão inicial, o responsável manteve o endereço Azure. Posteriormente confirmou acesso ao DNS corporativo; a evolução para cookies está documentada ao final. O inventário abaixo conserva o diagnóstico original.

## Inventário e ações

| Funcionalidade | Situação | Arquivo atual | Ação necessária |
| --- | --- | --- | --- |
| API ASP.NET Core, React/TypeScript e PostgreSQL compartilhados | IMPLEMENTADO | `src/OroBI.Api/Program.cs`, `src/OroBI.Web/src/App.tsx`, `src/OroBI.Infrastructure/Persistence/OroBiDbContext.cs` | Preservar arquitetura e módulos administrativos. |
| Login único com Identity e hash de senha | IMPLEMENTADO | `Infrastructure/Identity/LocalAuthenticationService.cs`, `Api/Auth/AuthEndpoints.cs` | Reutilizar; não criar autenticação paralela. |
| Lockout, rate limit e mensagens sem enumeração | IMPLEMENTADO | `Api/Auth/LoginRateLimiter.cs`, `LocalAuthenticationService.cs` | Preservar e testar regressões. |
| Seller com UUID interno | IMPLEMENTADO | `Domain/Sellers/Seller.cs` | Manter chave interna. |
| Código do vendedor no ERP | AUSENTE | `Seller.cs`, `OroBiDbContext.Identity.cs` | Adicionar ExternalId opcional, único quando informado, sem inferir códigos históricos por nome. |
| Vínculo UserSellerAccess N:N | IMPLEMENTADO | `Domain/Sellers/UserSellerAccess.cs`, `Infrastructure/Identity/DataAccessScope.cs` | Reutilizar. Vendedor tem exatamente um vínculo ativo; gestão pode ter vários. |
| Resolução central do vendedor no servidor | IMPLEMENTADO | `Application/Identity/IDataAccessScope.cs`, `DataAccessScope.cs` | Manter. Não confiar no SellerId enviado pelo React. |
| Recursos pessoais /me e rotas de gestão | IMPLEMENTADO | `Api/Portal/PortalEndpoints.cs`, `Api/Auth/CurrentUserEndpoints.cs` | Não duplicar endpoints. Preservar 403 para troca de identidade e gestão indevida. |
| Filtro comercial aplicado antes da agregação | IMPLEMENTADO | `Infrastructure/Portal/PortalQueryService.cs`, `Analytics/CommercialMovementQuery.cs` | Preservar aliases canônicos da fonte CSV, obtidos do vínculo autorizado. |
| Ocultação de custo, margem, salário e dados de colegas | IMPLEMENTADO | `Application/Portal/PortalAnalytics.cs`, `PortalClosing.cs` | Manter DTOs pessoais e regras de permissão. |
| Bloqueio do usuário e revogação de tokens | IMPLEMENTADO | `Api/Auth/SessionTokenValidation.cs`, `SellerPortalAccountEndpoints.cs` | Manter validação persistida em cada requisição. |
| Login de vendedor inativo | INSEGURO | `LocalAuthenticationService.cs` | Hoje recebe JWT mesmo sem vínculo com Seller ativo; consultas já negam dados. Impedir login e uso de sessão nesse estado, sem afetar administrador/diretoria. |
| Primeiro acesso obrigatório após senha temporária/reset | AUSENTE | `Infrastructure/Identity/ApplicationUser.cs`, `Api/Auth/SellerPortalAccountEndpoints.cs` | Adicionar MustChangePassword e bloqueio no backend; liberar somente identidade, troca de senha e logout até conclusão. |
| Troca de senha própria | PARCIAL | `SellerPortal.tsx` (Profile), `/me/change-password` | Reutilizar; acrescentar confirmação e fluxo de primeiro acesso. Revogar credenciais anteriores e entrar com a nova senha após sucesso. |
| Cadastro/reset administrativo | PARCIAL | `SellerPortalAccountEndpoints.cs`, `PortalAccounts.tsx` | Nome da pessoa, senha temporária gerada de forma criptográfica, indicação de troca obrigatória e visualização única da senha gerada. |
| Criação de acesso a partir do vendedor | PARCIAL | `PortalAccounts.tsx` | Pré-selecionar vínculo no formulário existente; abrir conta existente em vez de criar outra automaticamente. |
| Autocadastro e aprovação | IMPLEMENTADO | `SellerPortalRegistrationEndpoints.cs`, `auth/RegistrationForm.tsx` | Preservar; senha escolhida pelo próprio usuário não é temporária. |
| Último acesso administrativo | AUSENTE | `ApplicationUser.cs`, `PortalAccounts.tsx` | Gravar LastLoginAtUtc somente em login bem-sucedido e exibir no cadastro. |
| Auditoria de login/logout/senhas/bloqueio | IMPLEMENTADO | `LocalAuthenticationService.cs`, `SellerPortalAccountEndpoints.cs`, `AccountAuditEvents` | Preservar eventos existentes; nunca incluir senhas ou tokens nos detalhes. |
| Redirecionamento por perfil | IMPLEMENTADO | `Web/src/App.tsx` | Preservar vendedor/gestão → portal e administrador/diretoria → painel, com primeiro acesso anterior aos dados. |
| Sessão ao fechar/reabrir PWA | PARCIAL | `Web/src/auth/session.ts` | JWT de oito horas está só em sessionStorage. Acrescentar persistência opcional com expiração, limpeza entre abas e validação no servidor. |
| Cookies de primeira parte | PARCIAL | `Web/src/api/client.ts`, `Api/Program.cs` | SPA/API estão em domínios Azure distintos. Manter Bearer existente; não introduzir dependência de cookies terceiros incompatível com Safari. Documentar risco residual do armazenamento acessível a JavaScript e restringir conteúdo executável. |
| Expiração 401 e limpeza de dados | IMPLEMENTADO | `api/client.ts`, `App.tsx`, `auth/session.ts` | Preservar mensagem, expiração única e proteção contra resposta atrasada de sessão anterior. |
| Login responsivo/acessível | PARCIAL | `App.tsx`, `App.css` | Compactar mobile, campos ≥16px, toque adequado, recuperação administrativa e erro anunciado. |
| Home com receitas, comissão, PPP, prêmios e trocas | IMPLEMENTADO | `features/portal/PortalResults.tsx` | Reutilizar consultas e permissões. |
| Meta, realizado e quanto falta na home | PARCIAL | `PortalResults.tsx`, `PortalGoal` | Exibir campos já calculados/fornecidos pela API, distinguindo reais e clientes; não somar metas de unidades diferentes. |
| Menu mobile | IMPLEMENTADO | `SellerPortal.tsx`, `portal.css` | Aproveitar bottom navigation; promover Metas/Clientes, manter Mais e melhorar foco/Escape. |
| Fechamento, histórico e comissão estimada/aprovada | IMPLEMENTADO | `PortalClosingService.cs`, `PortalQueryService.cs`, `PortalResults.tsx` | Manter snapshot oficial e regras compartilhadas. |
| Manifest, ícones, standalone e service worker | IMPLEMENTADO | `public/manifest.webmanifest`, `public/service-worker.js`, `src/main.tsx` | Reutilizar; completar testes de contrato e navegador. |
| Cache de APIs privadas e fallback offline | IMPLEMENTADO | `service-worker.js`, `offline.html`, `PortalShared.tsx` | Manter APIs, requests autenticados e cross-origin fora do cache. Não criar dados comerciais offline. |
| Ajuda de instalação Android/iPhone | AUSENTE | `SellerPortal.tsx` | Ajuda contextual e botão de instalação quando o navegador oferecer; instruções manuais honestas. |
| Homologação em aparelhos Android/iPhone | PARCIAL | Evidências anteriores usam Chrome emulado | Testar os viewports solicitados; instalação física não pode ser certificada por emulação. |
| Integração operacional Firebird → BI | PARCIAL | `Application/Synchronization/IFirebirdCommercialReader.cs`, entidades/checkpoints | Contratos existem; não há reader concreto/worker operacional. Documentar dependência, sem ativar integração ou criar horários fictícios. |
| Última atualização a partir da origem | PARCIAL | `PortalQueryService.cs` (FreshnessAsync), `PortalResults.tsx` | Backend usa término de sincronização ou início de importação. Simplificar texto ao vendedor preservando significado do timestamp. |
| Documentação corporativa solicitada | PARCIAL | `docs/operations/seller-portal.md`, `README.md` | Criar `docs/SELLER_PORTAL.md` e `docs/AUTHORIZATION.md`, atualizar README e este diagnóstico. |

## Evidências existentes e testes a completar

`PortalEndpointsTests` já cobre os recursos obrigatórios, consultas próprias, tentativa de SellerId diferente, gestão indevida, permissões e ausência de custo. `DataAccessScopeTests` cobre vínculo, vendedor inativo e claims forjados. `PortalSessionTests` cobre usuário bloqueado, logout, troca de senha, alteração de roles e administração. `PortalQueryServiceTests` e testes de fechamento cobrem cálculos compartilhados, aliases e snapshot. `App.portal.test.tsx`, testes administrativos e `pwa.test.ts` cobrem navegação, offline, permissões e cache.

Completar regressões de senha temporária/primeiro acesso, vendedor inativo no login, ExternalId, último login, persistência entre reaberturas, recuperação administrativa, instalação e tamanhos 360×800, 390×844, 430×932, 768×1024 e desktop. Executar a baseline antes de alterações e as suítes pertinentes em cada etapa.

## Limites e decisões da primeira entrega

O código ERP não será adivinhado, nem mudará o filtro comercial dos fatos CSV existentes. Nenhuma regra de venda, margem, trocas, PPP, metas, comissão ou fechamento será alterada. A arquitetura atual de HTTPS Azure será mantida por decisão do responsável. Sessão persistente é opcional, tem validade máxima de oito horas e não armazena senha; tokens seguem revogáveis no backend. Cookies HttpOnly de primeira parte exigiriam mudança de origem/proxy/DNS; cookies de terceiros não são alternativa confiável no iPhone. A implementação não implica publicação, alteração de contas reais ou configuração de infraestrutura ainda não revisada.

## Resultado da primeira implementação

| Funcionalidade auditada | Situação final local | Evidência principal |
| --- | --- | --- |
| Login de vendedor inativo/sem vínculo válido | IMPLEMENTADO | `SellerLoginEligibility` compartilhado pela emissão e validação JWT; testes de vínculo ausente, inativo, duplicado e Seller inativo. |
| Primeiro acesso e reset obrigatório | IMPLEMENTADO | Flag persistida, middleware com bloqueio padrão, formulário compartilhado, confirmação e reautenticação após troca. |
| Código ERP e metadados de acesso | IMPLEMENTADO | ExternalId opcional único, criação/edição sem alterar UUID/alias; nome e último login no formulário administrativo. |
| Senha temporária administrativa | IMPLEMENTADO | Geração criptográfica, resposta específica e descarte ao fechar/trocar sessão; sem senha em listagem, storage ou auditoria. |
| Autocadastro existente | IMPLEMENTADO | Aprovação preservada; senha escolhida pelo usuário não exige troca até um reset. |
| Sessão lembrada no Azure atual | IMPLEMENTADO | Opt-in de até oito horas, validação antes de abrir dados, expiração ociosa/401 e isolamento de respostas antigas/abas. |
| Cookie HttpOnly | PARCIAL | Não introduzido: Azure SPA/API continuam cross-site. Bearer em armazenamento JavaScript tem risco residual XSS documentado; CSP adicionada. |
| Recuperação e login mobile | IMPLEMENTADO | Ajuda administrativa, erro acessível, campos 16px, botões de toque e formulário compacto. |
| Navegação e PWA | IMPLEMENTADO | Início/Vendas/Metas/Clientes/Mais, foco/Escape, Perfil compartilhado, ajuda Android/iOS e evento de instalação quando disponível. |
| Meta/realizado/falta e atualização | IMPLEMENTADO | Campos da API com unidade correta; início da importação identificado como início, sem horários fabricados. |
| Worker Firebird e instalação física | PARCIAL | Dependências reais preservadas; nenhuma ativação ou certificação de aparelho físico feita nesta entrega. |
| Documentação solicitada | IMPLEMENTADO | README, SELLER_PORTAL.md, AUTHORIZATION.md e esta auditoria; guia operacional anterior aponta a política atual. |

### Verificação automatizada

Baseline antes das alterações: **369 testes .NET** e **104 Web** aprovados. Resultado final local: **382 .NET** (153 API, 78 Application, 151 Infrastructure) e **135 Web em 26 arquivos**, sem falhas. O build de produção `npm.cmd run build` passou. Lint saiu com código 0, sem erros e com um aviso `react(set-state-in-effect)` no efeito de análise de trocas já existente em `App.tsx`.

Os testes de segurança existentes cobrem os recursos individuais e tentativas de mudar identidade, permissões, ausência de custo/margem/salário e consistência com o fechamento aprovado. Foram acrescentadas regressões para senha temporária, flag alterada depois da emissão de JWT, novas rotas privadas bloqueadas, exceções por método/prefixo/caixa/barra, geração/reset, último login, código ERP, recuperação, reabertura/expiração e descarte de credenciais entre sessões.

A revisão independente do backend não identificou bloqueio pendente. A revisão Web encontrou a expiração pelo relógio que limpava storage sem encerrar a tela; uma regressão reproduziu a falha e passou após a correção. Também se testou expiração sem novas chamadas à API. Duas asserções antigas que presumiam renderização administrativa antes da identidade passaram a aguardar a validação; o comportamento de segurança permanece coberto separadamente.

A migração aditiva `20260906002605_AddSellerMobileAccess` e seu SQL idempotente estão preparados. A checagem EF não encontrou alterações de modelo pendentes. Os testes usam EF InMemory e não comprovam execução relacional da migração. Contas antigas recebem flag false; nenhum código ERP foi inferido e nenhum fato comercial foi modificado.

Evidências brutas locais ficam em `.worktrees/mobile-evidence/` no worktree da tarefa, incluindo TRX de baseline/final e capturas sintéticas. Perfis de navegador, tokens de teste e artefatos bin/obj não fazem parte do código a publicar.
### Navegador e apresentação

Chrome 152 com o build de produção local e os cabeçalhos CSP do Azure: **cinco tamanhos e 42 capturas** — 360×800, 390×844, 430×932, 768×1024 e 1440×900. Login → troca obrigatória → novo login → portal foi percorrido em todos, sem chamadas a dados antes da troca. Home, vendas, metas, clientes, menu e perfil não apresentaram rolagem horizontal da página. O menu restaurou o foco após Escape.

Após inspeção, foram corrigidos o alvo do botão mostrar senha, os campos/toques do login em tablet e o excesso de altura no login de 360px. Os alvos medidos ficaram com pelo menos 44px; campos de celular/tablet com pelo menos 16px. No login de 360×800, Entrar e as duas ações de ajuda/cadastro ficaram visíveis. O desktop conserva o tamanho anterior dos campos do portal.

O manifesto foi interpretado sem erros. Service worker ativado e controlando a página; cache limitado aos sete arquivos públicos esperados. Ao interromper o servidor local, a navegação entregou o documento offline sem resultados comerciais. Nenhuma violação CSP foi registrada; o único erro de rede no relatório decorre da desconexão simulada. O estado de aplicativo instalado foi emulado; os contextos isolados do Chrome reportaram a restrição de instalação em modo anônimo. Isso não certifica instalação real Android/iPhone ou funcionamento físico standalone.

A validação usou somente fixtures sintéticas no servidor/interceptação de teste. Elas não foram adicionadas à aplicação, à API oficial ou ao banco real. Relatório detalhado: `.worktrees/mobile-evidence/report.json`; capturas: arquivos PNG no mesmo diretório ignorado. A homologação HTTPS e a migração PostgreSQL permanecem etapas de ativação.
## Evolução: domínio corporativo e cookie HttpOnly

A confirmação posterior de acesso ao DNS substituiu a restrição inicial. O responsável determinou que o BI antigo em bi.oroleite.com.br continue separado por enquanto. Consultas de leitura identificaram esse domínio no aplicativo orlt-bi; o portal atual é orobi-web. A implementação usa portal-bi.oroleite.com.br e api-bi.oroleite.com.br, configuráveis. Os dois novos nomes retornaram inexistentes no DNS consultado. Nenhum vínculo antigo foi alterado.

A API agora suporta __Host-OroBI.Session com HttpOnly, Secure, SameSite=Strict, Path=/, sem Domain, prazo absoluto máximo de oito horas e validação Identity/SecurityStamp existente. O modo é desabilitado por padrão. Login cookie retorna somente metadados; a UI não armazena JWT/senha e valida /me antes de resultados, inclusive ao retomar. Primeira senha, revogação, escopo individual e Azure Bearer foram preservados. Cookie não exige nova migração.

O controle CSRF exige HTTPS, Host, Origin exata e cabeçalho customizado. As regressões negam origem antiga bi, sibling hostil, Origin ausente/null, cabeçalho ausente/incorreto, HTTP, Host indevido e configurações wildcard/IP/ponto final. Authorization explícito seleciona Bearer e não usa cookie como alternativa. Logout/troca própria apagam cookie após sucesso; 401 genérico não emite exclusão. No-store também cobre caixa alternativa de /api.

Verificação final nesta evolução: **425 testes .NET** (196 API, 78 Application, 151 Infrastructure), **150 Web em 28 arquivos**, build produção aprovado, lint sem erros e com o aviso preexistente do efeito de análise de trocas. Compilação Bicep aprovada; **sete testes do script de implantação**, com Azure simulado, verificam opt-in, configuração insegura, exigência de certificado, preservação de vínculos habilitando/desabilitando e interrupção quando a leitura falha.

A revisão corrigiu a captura antecipada de configuração JWT/CORS para que emissão e validação usem a configuração final, reproduzida por WebApplicationFactory. A regressão de instalação PWA existente foi sincronizada com os efeitos React antes de emitir beforeinstallprompt; nenhuma lógica de instalação foi alterada. Regressões corporativas cobrem bootstrap, storage bloqueado/legado, reabertura, primeiro acesso, expiração, foco/bloqueio, saída pendente/falha, cadastro e 401 atrasado durante reautenticação.

**Chrome HTTPS local: 26/26 verificações**, com cinco capturas em 360×800. Cookie real do navegador apresentou os atributos esperados, login JSON sem JWT, storage sem credencial, fechamento/reabertura de aba conservando cookie e /me antes de dados. Troca obrigatória e novo login, logout/exclusão e reabertura sem sessão foram percorridos. Nenhuma violação CSP, exceção de runtime ou rolagem horizontal. Relatório ignorado em .worktrees/cookie-evidence/report.json; build testado index-BU1STKhj.js.

O servidor Node do smoke usa identidades sintéticas e emula o contrato HTTP; a aplicação ASP.NET real foi verificada independentemente pelos testes de integração. O certificado temporário só foi aceito pelo SPKI específico no perfil isolado, sem alterar confiança do sistema ou DNS real. Essa prova de reabertura de aba não certifica instalação e encerramento de processo de PWA em iPhone/Android físicos.

Limites operacionais: logout é global para a conta; logout/login simultâneos em abas distintas podem exigir repetir o login, pois o navegador compartilha o cookie. Falha de rede no logout informa que o cookie pode permanecer e oferece nova tentativa. Ativação de DNS/certificados, publicação, migração PostgreSQL e aparelhos físicos permanecem pendentes. Consulte [Ativação corporativa](operations/corporate-session-activation.md); o guia preserva o BI antigo e os certificados da API durante implantação.
