# Completar o acesso mobile dos vendedores

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** completar o fluxo corporativo de acesso, primeiro login, autorização e PWA, preservando o BI publicado e o autocadastro aprovado anteriormente.

**Architecture:** reutilizar Identity, JWT revogável de oito horas, UserSellerAccess, IDataAccessScope, endpoints e componentes existentes. Acrescentar apenas metadados de conta/vendedor e enforcement de troca obrigatória. O endereço Azure atual é requisito confirmado: persistência opcional do Bearer existente, com prazo explícito, validação no servidor e proteção de conteúdo, sem cookies de terceiros.

**Tech Stack:** ASP.NET Core 10, Identity/EF Core/PostgreSQL, React/TypeScript/Vite, xUnit/Vitest, Chrome local com dados sintéticos.

**Spec:** `docs/SELLER_PORTAL_AUDIT.md` e pedido completo do responsável (68 seções).

## Restrições globais

- Auditar primeiro; diagnóstico entregue antes de código. Não duplicar autenticação, entidades, páginas, endpoints nem regras comerciais.
- Não modificar vendas, margem, PPP, metas, trocas, comissão ou fechamento. Reutilizar cálculos existentes.
- Manter cadastro próprio pendente de aprovação e o endereço HTTPS Azure atual.
- Não criar usuários reais, alterar históricos, ativar Firebird ou publicar sem concluir a implementação/testes e revisar a ativação.
- Manter alterações em `.worktrees/seller-mobile-access`; outras alterações locais não pertencem a esta entrega.
- Não guardar senhas, senhas temporárias ou respostas privadas em storage/cache/logs. Senha temporária gerada só retorna uma vez ao administrador.

## 1. Auditoria e baseline

- [x] Inventariar backend, vínculos, endpoints, Web, PWA, sincronização e testes; registrar estados em `docs/SELLER_PORTAL_AUDIT.md` e entregar resumo ao responsável.
- [x] Confirmar baseline .NET e Web sem falhas no worktree antes de código.

## 2. Backend de acesso e metadados

**Arquivos:** ApplicationUser, LocalAuthenticationService, SessionTokenValidation, CurrentUserEndpoints, SellerPortalAccountEndpoints, Program, Seller, configuração EF de identidade e testes Auth/Identity/Portal.

**Contratos produzidos:**

```csharp
// Campos adicionados à entidade existente, sem substituir vínculos.
public bool MustChangePassword { get; set; }
public DateTimeOffset? LastLoginAtUtc { get; set; }
// Seller mantém UUID; código ERP textual opcional, trim/upper, máximo 64, único quando informado.
public string? ExternalId { get; set; }
```

```text
LoginResponse e GET /api[/v1]/me acrescentam mustChangePassword: bool.
GET admin/users acrescenta lastLoginAtUtc e mustChangePassword; registrationName é o nome já existente.
POST admin/users aceita name opcional, password manual OU generateTemporaryPassword=true.
POST admin/users/{id}/reset-password aceita newPassword manual OU generateTemporaryPassword=true.
Senha gerada: retornar temporaryPassword somente nessa resposta. Criação e reset administrativos sempre exigem troca.
POST admin/sellers aceita externalId opcional.
PUT admin/sellers/{id}/external-id aceita { externalId: string | null }.
GET admin/sellers retorna externalId; não muda nomes/aliases comerciais ao editar código.
```

- [x] Escrever regressões: senha temporária exige troca; usuário não pode contornar via /me/dashboard, gestão ou APIs administrativas em ambos os prefixos; troca limpa flag e revoga token; senha igual à atual não conclui troca; reset reativa flag; geração sem senha em audit; LastLoginAt somente em sucesso; Seller inativo/vínculo inválido impede login e sessão; código ERP único/opcional preserva UUID e histórico.
- [x] Rodar regressões e confirmar falhas pelas lacunas.
- [x] Implementar elegibilidade de login central compartilhada entre emissão e validação de token, preservando administração/diretoria.
- [x] Projetar o estado persistido em claim na validação; middleware bloqueia requests autenticadas pendentes com `403 { code: "password_change_required", error: "Crie uma nova senha para continuar." }`. Permitir apenas GET `/api[/v1]/me`, POST `/api[/v1]/me/change-password` e POST `/api[/v1]/auth/logout` até concluir. Normalizar caixa/barra final; nova rota privada deve ficar bloqueada por padrão.
- [x] Reutilizar troca de senha com Identity; manter resposta 204. Frontend fará novo login com a senha recém-definida em memória.
- [x] Senha temporária criptográfica com classes exigidas pelo Identity; nunca logar payload de senha. Metadados em migração aditiva, contas existentes com flag false.
- [x] Confirmar regressões e suíte .NET; gerar migração/SQL e verificar modelo sem mudanças pendentes.

## 3. Login, sessão e primeiro acesso na Web

**Arquivos:** App.tsx, auth/session.ts, api/client.ts, novo auth/ChangePasswordForm.tsx, CSS de login, testes novos de primeiro acesso/sessão e testes existentes pertinentes.

**Interface compartilhada:**

```tsx
type ChangePasswordFormProps = {
  token: string;
  requiredChange?: boolean;
  onChanged: (newPassword: string) => void | Promise<void>;
};
```

O formulário chama o endpoint existente, confirma senha, limpa campos e informa erro acessível. Profile utiliza o mesmo componente e encerra sessão como hoje; primeiro acesso usa `onChanged` para autenticar com a nova senha e entrar no destino do perfil.

- [x] Testes antes da implementação: gate impede montagem de dados, troca bem-sucedida entra no portal, confirmação inválida não envia, reload observa flag em /me, 403 específico ativa gate, sessão persistida reabre dentro de oito horas e expira, logout limpa ambos os storages e abas, resposta antiga não derruba sessão nova.
- [x] Acrescentar checkbox explícito de lembrar sessão, desmarcado por padrão. Sem opção, conservar sessionStorage; com opção, guardar token/expiração no localStorage, sem senha. Limitar prazo ao emitido pelo servidor e no máximo oito horas. Validar /me antes de dados.
- [x] Preservar logout e 401; observar remoção de sessão entre abas. Não tratar 403 comum como expiração.
- [x] Reutilizar login com erro genérico anunciado e ajuda de recuperação pelo administrador. Campos mobile ≥16px e alvos ≥44px.
- [x] Endurecer cabeçalhos de conteúdo compatíveis com assets atuais, sem quebrar API Azure, fontes/ícones e estilos legítimos.
- [x] Executar Web relevante e build; verificar painel administrativo e autocadastro.

## 4. Administração existente e experiência PWA

**Arquivos:** PortalAccounts.tsx, SellerPortal.tsx, PortalResults.tsx, portal.css, tipos existentes; novo componente de ajuda PWA e testes focados.

- [x] Testar e completar cadastro/exibição/edição de ExternalId, nome da pessoa, último acesso e troca obrigatória nos formulários existentes.
- [x] Reutilizar criação de conta com botão de gerar senha temporária; exibir resposta uma vez e permitir descartar/copiar por ação do usuário. Estado não persiste nem entra em auditoria. Reset gerado reativa troca obrigatória.
- [x] Atalho a partir de Seller pré-seleciona formulário de conta. Se conta de vendedor já estiver associada, abrir edição em vez de criar outra automaticamente.
- [x] Reutilizar ChangePasswordForm no Perfil, acrescentar instalação ao Perfil/Mais e promover Início/Vendas/Metas/Clientes/Mais na navegação conforme permissões. Menu com foco e Escape.
- [x] Ajuda Android/iOS/instalado; `beforeinstallprompt` só permite botão quando evento existir, caso contrário instruções manuais; não prometer instalação automática.
- [x] Home mostra meta, realizado e quanto falta por unidade a partir dos dados existentes; usa timestamp de origem e texto sem detalhes técnicos do ERP. Não chamar início de importação de conclusão de sincronização.
- [x] Validar manifesto, registro SW, cache privado excluído, offline e frontend relevante.

## 5. Integração, revisão e documentação

- [x] Rodar testes de autorização existentes para os oito módulos obrigatórios, regressões novas e consistência admin/portal usando os mesmos serviços.
- [x] Executar suíte .NET, Web, build de produção e lint. Não ampliar testes repetidamente sem mudança/falha que justifique.
- [x] Chrome local: 360×800, 390×844, 430×932, 768×1024 e desktop; login, primeiro acesso, navegação, sessão, instalação/standalone emulado, manifest/SW/offline e nenhuma rolagem horizontal. Usar só dados sintéticos e registrar limites de emulação.
- [x] Revisão independente de enforcement, sessão, senhas temporárias, isolamento e migração; corrigir achados comprovados antes de concluir.
- [x] Atualizar README, `docs/SELLER_PORTAL.md`, `docs/AUTHORIZATION.md` e auditoria com implementado/testado e dependências reais (códigos ERP, integração Firebird, aparelhos físicos e eventual publicação).
