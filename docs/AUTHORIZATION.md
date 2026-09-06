# Autorização e sessões do Portal do Vendedor

## Fonte da autorização

A API utiliza ASP.NET Core Identity, roles persistidos, `Seller` e `UserSellerAccess`. O `DataAccessScope` resolve o vendedor e suas permissões no servidor antes de consultar ou agregar movimentos. UUID e código ERP cumprem funções distintas; `ExternalId` é metadado de correspondência, nunca credencial ou prova de acesso.

| Perfil | Escopo |
| --- | --- |
| Vendedor | Exatamente um vínculo ativo com vendedor ativo. Sem seleção livre de identidade. |
| Gestor/Gerente | Vendedores dos vínculos ativos e suas permissões. |
| Administrador/Diretoria | Visão global prevista pelo BI. Administração de contas continua exclusiva de Administrador. |

Não atribuir perfis mistos como atalho de acesso: a regra de elegibilidade restringe qualquer conta que inclua Vendedor, salvo Administrador/Diretoria, a exatamente um vínculo ativo. A UI administrativa atribui um único perfil por operação.

Os nomes/aliases usados na base CSV são obtidos do cadastro autorizado. Parâmetros do React, claims antigos de vendedor ou `ExternalId` informado pelo cliente não substituem os vínculos persistidos. Rotas de analytics, folha e fechamento administrativo completo seguem exclusivas da visão global. DTOs pessoais não contêm salário, custo, margem ou remuneração individual de colegas.

## Endpoints existentes

| Método e rota | Finalidade |
| --- | --- |
| `POST /api/v1/auth/login` | Autentica e retorna Bearer, validade, perfis e `mustChangePassword`. |
| `GET /api/v1/me` | Identidade, vínculos/permissões e exigência persistida de troca. |
| `POST /api/v1/me/change-password` | Troca própria validada pelo Identity; revoga token anterior e limpa exigência. |
| `POST /api/v1/auth/logout` | Revoga sessões da conta por SecurityStamp. |
| `GET /api/v1/me/dashboard` | Resumo individual. |
| `GET /api/v1/me/sales` | Vendas próprias paginadas. |
| `GET /api/v1/me/customers`, `/customers/{customerCode}` | Clientes e detalhe dentro do mesmo escopo. |
| `GET /api/v1/me/products`, `/brands` | Rankings individuais permitidos. |
| `GET /api/v1/me/goals`, `/ppp`, `/trades` | Metas, PPP e trocas permitidos. |
| `GET /api/v1/me/commission`, `/closings`, `/closings/history` | Comissão, fechamento pessoal e histórico oficial. |
| `GET /api/v1/management/sellers` | Vendedores disponíveis à gestão. |
| `GET /api/v1/management/sellers/{sellerId}/{resource}` | Mesmo recurso com escopo de gestão autorizado. |

Login, identidade, troca e logout preservam também os aliases `/api/...`. Recursos de negócio pessoais usam `/api/v1/me/...`. Tentativas de fornecer `seller`/`sellerId` nas consultas pessoais são rejeitadas com 403; a gestão valida o UUID solicitado contra os vínculos. As permissões de receita/clientes, metas, PPP, trocas, comissão e prêmios são verificadas também em respostas compostas. Valores ocultados não viram zero fictício.

## Primeiro acesso e administração

`ApplicationUser.MustChangePassword` é false para contas existentes e para autocadastro com senha escolhida pelo usuário. Criação e reset administrativos, com senha manual ou gerada, definem true. A geração usa `RandomNumberGenerator`; o texto retorna somente na resposta específica de criação/reset e no estado efêmero do formulário administrativo.

`SessionTokenValidation` recompõe roles e o claim `must_change_password` a partir do banco em cada request. O middleware seguinte bloqueia por padrão requests autenticadas em `/api` quando a troca estiver pendente. A única lista de exceções é GET identidade, POST troca e POST logout, nos dois prefixos. Caixa e barra final são normalizadas; método diferente não amplia a exceção. A resposta é `403 { code: "password_change_required", error: "Crie uma nova senha para continuar." }`.

A UI valida `/me` antes de montar telas privadas, apresenta a troca obrigatória e reage ao 403 específico. A API é responsável pelo bloqueio mesmo que a UI seja contornada. A troca de senha exige a senha atual e uma nova senha diferente, limpa a flag e revoga o token anterior. O primeiro acesso faz novo login com a senha recém-definida apenas em memória; nenhuma senha é gravada no navegador.

Operações administrativas existentes foram estendidas: criar usuário aceita `name` e `password` ou `generateTemporaryPassword`; reset aceita `newPassword` ou geração. A criação gerada retorna 201 com `temporaryPassword`; reset gerado retorna 200 com esse campo; reset manual retorna 204. A listagem mostra nome, estado de troca e `LastLoginAtUtc`, nunca senha. `PUT /api/v1/admin/sellers/{id}/external-id` edita o código ERP sem alterar UUID, alias ou histórico.

## Sessão no endereço Azure

SPA e API permanecem em domínios Azure distintos por decisão do responsável. A implementação conserva o Bearer existente, sem depender de cookies de terceiros. **Não há cookie HttpOnly nesta solução.** A opção explícita de manter acesso usa `localStorage` para token e expiração; sem ela, apenas `sessionStorage`. O limite é a validade emitida pela API e no máximo oito horas desde o login. Não há refresh token ou renovação deslizante.

O armazenamento é acessível ao JavaScript da origem: uma falha XSS pode expor o Bearer. A preferência por manter o endereço atual não elimina esse risco. `staticwebapp.config.json` restringe scripts à própria origem, proíbe scripts inline/eval, objetos e enquadramento e limita conexões à origem e à API oficial. Estilos inline continuam permitidos para os componentes existentes; Google Fonts permanece permitido apenas para CSS/fontes. Isso reduz a superfície de conteúdo, mas não torna `localStorage` equivalente a HttpOnly.

O token carrega `session_version`; em cada request o servidor confere conta ativa, estado pendente, elegibilidade do vendedor, SecurityStamp e roles persistidos. Bloqueio, logout, senha e alterações de acesso revogam sessões. Um vendedor com vínculo ausente/múltiplo/inativo ou Seller inativo não recebe sessão e não consegue continuar usando uma antiga. O backend mantém lockout e rate limiting existentes.

A UI remove resultados e armazenamento ao sair, receber 401 ou vencer a validade, inclusive ao retomar uma aba suspensa. Eventos de outra aba encerram a sessão local quando a persistência é removida ou substituída. Respostas atrasadas de um token anterior não encerram uma sessão nova. Logout sem rede limpa o dispositivo e informa que a revogação remota não foi confirmada.

`LastLoginAtUtc` e eventos de auditoria não contêm senha ou Bearer. Criação, reset e troca usam as transações existentes; a migração é aditiva. O service worker exclui APIs, requisições autenticadas e origens externas do cache. Dados privados não ficam disponíveis offline.

## Evidências e limites

Testes `PortalEndpointsTests`, `DataAccessScopeTests`, `PortalSessionTests`, `PortalMobileAccessTests` e `RequiredPasswordChangeTests` cobrem escopo, manipulação de identidade, permissões, bloqueio, flag persistida após emissão, exceções por método/rota e revogação. Testes comerciais existentes preservam consistência e snapshots; os testes Web cobrem primeiro acesso, reabertura, expiração, corrida de sessão e descarte de senha temporária.

Integração de API usa JWT real com EF InMemory. A geração de SQL e a checagem de modelo não substituem aplicação/homologação PostgreSQL. Nenhuma base ou conta real foi alterada nesta entrega. Consulte [Auditoria](SELLER_PORTAL_AUDIT.md) para resultados de navegador e dependências de ativação.
