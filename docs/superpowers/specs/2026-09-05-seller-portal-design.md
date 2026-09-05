# Portal do Vendedor — fase 3

Origem: Adendo ao Prompt fase 3.pdf, 38 páginas, itens 107–183. Autorização: após a análise integral, o usuário pediu “vamos implementar”. A sequência proposta na análise está aprovada. Implementação local, sem publicação automática nem ativação do Firebird.

## Arquitetura e limites

Reutilizar React/TypeScript/Vite, ASP.NET Core Identity/API, PostgreSQL e os serviços comerciais. Criar `/portal` na SPA e endpoints pessoais `/api/v1/me/*`. Separar dados retornados ao vendedor dos contratos administrativos. Não implementar app nativo, push, carteira inventada ou cache de respostas autenticadas. O CSV é a origem disponível; a tela identifica origem e data da importação, sem afirmar sincronização Firebird inexistente.

## Identidade e autorização

Criar Seller com Guid estável, Name, ImportedName canônico e IsActive; UserSellerAccess vincula usuário e vendedor, com auditoria e ativação. Manter os nomes nos fatos importados nesta migração, mas todo acesso externo usa SellerId e resolve o nome importado exclusivamente no servidor. Rejeitar colisões de aliases no cadastro. Um Vendedor tem exatamente um vínculo ativo; Gestor/Gerente têm N vínculos; Administrador/Diretoria têm visão global. Gestores sem vínculo não ganham acesso global implícito.

Contrato compartilhado: `OroBI.Application.Identity.IDataAccessScope.ResolveAsync(ClaimsPrincipal user, Guid? requestedSellerId, CancellationToken cancellationToken)` retorna `SellerAccess?`, com `SellerId`, `Name`, `ImportedName` e `Permissions`. Ausência, desativação ou pedido fora de escopo retorna null. O vendedor com ID solicitado diferente é negado. Os endpoints `/me` rejeitam tentativas de selecionar outro vendedor; os gerenciais usam a mesma resolução.

`SellerPortalPermissions` contém CanViewRevenue, CanViewCommission, CanViewPrize, CanViewPPP, CanViewGoals, CanViewTrades, CanViewCustomers, todos inicialmente true; custo/margem e ranking não são expostos. Permissões são aplicadas no backend inclusive em respostas compostas.

JWT recebe versão de sessão vinculada ao SecurityStamp do Identity; validação consulta usuário ativo e stamp em cada requisição. Logout, troca/reset de senha e desativação revogam sessões. Tokens legados sem versão exigem novo login. Senhas nunca persistem no navegador. Manter bearer no sessionStorage como estratégia existente, sem refresh persistente; HTTPS, expiração e Cache-Control no-store nas respostas autenticadas. Registrar eventos de conta sem senha ou token.

Administração: endpoints e interface para cadastrar vendedores, criar usuários/vínculos, ativar/desativar, alterar permissões e redefinir senha. Sem criar credenciais reais automaticamente.

Atualização autorizada em 05/09: após pedir para prosseguir com a ativação, o usuário esclareceu que os vendedores farão seu próprio cadastro. O login oferece nome, e-mail, senha e confirmação; o cadastro cria uma conta inativa e pendente, sem papel ou vínculo comercial. O servidor não aceita privilégios solicitados pelo cadastro público e não emite sessão. Um administrador confere a identidade, seleciona exatamente um vendedor ativo e suas permissões e aprova a conta em uma transação auditada. A ativação comum não substitui essa aprovação. Duplicatas não modificam contas existentes; validação e limites de requisições protegem o endpoint público. Não há envio de e-mail nem validação automática de vínculo com o ERP nesta versão.

## Consultas e regras

Serviço de consultas do portal usa um ImportedName já autorizado, CommercialMovementQuery antes da materialização, deduplicação existente e calculadoras oficiais. Endpoints agregados para dashboard, produtos, marcas, metas, PPP e trocas; vendas paginadas; clientes e detalhes limitados às vendas daquele vendedor. Não inferir carteira de clientes sem compra.

Mês atual como padrão, filtros por data/cliente/produto/marca, intervalo válido e paginação limitada. Metas e faixas usam GoalPayoutCalculator; próxima faixa e valor faltante calculados no backend. Comparações e projeções somente com base suficiente e regra definida; neste MVP não mostrar previsão baseada em calendário não homologado.

## Fechamento oficial

Estados EmApuracao, EmConferencia, Aprovado. Administrador registra conferência e aprova snapshot mensal; aprovação exige valores calculáveis e congela o resultado. Snapshot guarda o contrato comercial completo apenas no servidor; vendedor recebe projeção segura sem salário, documentos de colegas ou prêmios individuais de equipe. Reabertura não integra o MVP: aprovado é imutável e retorna conflito em nova transição. Administração consulta o snapshot oficial pela mesma API utilizada no portal, com autorização gerencial. Estimativas usam cálculo atual e indicação explícita. Histórico distingue meses disponíveis e aprovações.

## Interface e PWA

Após testes de isolamento, implementar login por perfil, dashboard, vendas, clientes/detalhe, produtos, marcas, metas/prêmios, PPP, trocas, comissão, fechamento/histórico e perfil/troca de senha. Navegação inferior em 360/390/430/768px e desktop, tema compatível com identidade atual. Estados de carregamento, vazio, sem permissão, falha e offline. PWA com manifest, ícones locais e Service Worker que trata apenas navegação/fallback e arquivos públicos; nunca cachear API ou respostas autenticadas. Logout limpa estado comercial.

## Aceite e verificação

Testes reais de isolamento com dois vendedores, gestor autorizado/não autorizado, token revogado/desativado, ausência de custos e dados de colegas. Conferir números com calculadoras existentes, próxima faixa, paginação/filtros, snapshot que permanece igual após alterar fatos, transições inválidas, permissões em DTOs compostos. Testes Web dos fluxos principais, sessão e navegação, build/typecheck e suite existente. Registrar limitações de Firebird e testes de dispositivos físicos sem afirmar homologação inexistente.
