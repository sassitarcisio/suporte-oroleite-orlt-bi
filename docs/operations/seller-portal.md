# Portal do Vendedor — operação da fase 3

Implementação local do MVP do adendo (itens 107–183). A SPA oferece `/portal` na mesma aplicação e usa `/api/v1/me/*`; gestores usam `/api/v1/management/sellers/{sellerId}/*` após selecionar um vendedor autorizado.

## Ativação

1. Aplicar as migrations `AddSellerPortal` e `AddSellerSelfRegistration` no banco BI pelo mecanismo de migração já existente. Elas adicionam vendedores, vínculos/permissões, auditoria, snapshots e estado do cadastro; preservam fatos comerciais e contas existentes.
2. Publicar API e SPA da mesma versão pelo processo existente. As migrations devem preceder a nova API.
3. Entrar novamente como Administrador. Tokens anteriores à versão com `session_version` deixam de ser aceitos.
4. Abrir **Portal do Vendedor → Acessos**. Cadastrar cada vendedor com nome de apresentação e nome importado correspondente aos fatos comerciais. Aliases conhecidos são normalizados no backend; duas identidades que representam o mesmo vendedor são rejeitadas.
5. Os vendedores acessam **Criar minha conta** na tela de entrada e informam nome, e-mail e senha. O cadastro fica pendente, sem sessão, papel ou vínculo comercial. Em **Portal do Vendedor → Acessos**, o administrador confere a identidade, seleciona exatamente um vendedor ativo e suas permissões e aprova o cadastro. Gestor/Gerente continuam sendo cadastrados administrativamente e podem ter vários vínculos. Nenhuma conta real é criada pela migration.
6. Conferir um período com dados: receita, metas, PPP, trocas e comissão. Selecionar o vendedor no portal gerencial para iniciar conferência e aprovar o fechamento.

Administrador e Diretoria têm visão global. Gestor e Gerente passam a ter apenas os vendedores vinculados, inclusive se anteriormente usavam a interface administrativa completa. As rotas legadas de analytics, folha e fechamento completo ficam restritas à visão global; os demais perfis usam os contratos seguros do portal.

## Fechamento e histórico

Antes da aprovação, comissão e prêmios são estimativas. Administrador inicia a conferência e aprova um mês com movimentos e configuração calculável. A aprovação registra autor, data e o resultado oficial; revisões concorrentes são protegidas por versão e índice único. As leituras de aprovação usam transação Repeatable Read em PostgreSQL para não misturar importações durante o cálculo.

O snapshot aprovado é imutável nesta versão. Novas importações ou configurações não alteram o fechamento aprovado, nem suas metas/PPP oficiais. A consulta individual administrativa também usa esse snapshot. Analytics de vendas continuam representando a base comercial atual; a folha consolidada legada mantém seu cálculo operacional e não é um documento de aprovação de snapshots individuais.

O portal recebe apenas a projeção pessoal: sem salário, custo, margem, documentos de colegas ou premiação individual da equipe. Fechamentos especiais preservam a própria remuneração calculada, mas ocultam a receita e as trocas de empresa/equipe fora do escopo individual.

Desativar uma conta ou vendedor revoga sessões afetadas. Remover um vínculo o mantém inativo para preservar o histórico. Administradores continuam podendo consultar fechamentos antigos. Reabertura/correção de snapshot aprovado exige um fluxo futuro explícito, não edição direta da base.

## Sessões e permissões

O autocadastro usa `POST /api/v1/auth/register`. A resposta aceita não autentica nem confirma se o e-mail já existe; cadastros repetidos não alteram a conta anterior. A aprovação usa `POST /api/v1/admin/users/{id}/approve-registration` e cria somente o perfil Vendedor. Contas pendentes não podem contornar a aprovação pelas ações comuns de ativação ou edição de acesso. A identidade e a correspondência com o vendedor devem ser conferidas pelo administrador; não há envio ou verificação automática de e-mail nesta versão.

O cadastro limita três tentativas por e-mail em 15 minutos e 60 tentativas totais por minuto, por instância da API, com resposta 429 e Retry-After. O limite não é distribuído entre réplicas. A senha segue a política Identity: de 8 a 128 caracteres no cadastro, com maiúscula, minúscula, número e símbolo.

JWT expira em 8 horas e o servidor confere usuário ativo, SecurityStamp e papéis persistidos em cada requisição. Logout, troca/reset de senha e alterações de acesso revogam sessões. Tokens são mantidos apenas em sessionStorage; senhas não são persistidas no navegador. Login bem-sucedido/falho e operações de conta registram eventos sem senha nem token.

Permissões são aplicadas nos endpoints e em respostas compostas. Vendas/clientes exigem visualização de receita e clientes; detalhes de trocas também exigem ambas, além de trocas. Comissão, prêmios, PPP e metas têm controles próprios. Valores suprimidos não são substituídos por zero. Custo, margem, salário e ranking financeiro de colegas não integram o portal.

## Dados e PWA

A fonte disponível é CSV. A tela distingue data de início da importação concluída de uma sincronização Firebird concluída, quando esta existir. O horário do filtro padrão usa America/Sao_Paulo. Clientes são compradores observados no período, não a carteira completa do ERP. Não há clientes sem compra inventados, projeções de dias úteis sem calendário homologado, push, aplicativo nativo ou pedidos offline.

Vendas são paginadas em até 100 itens. Listas de clientes, produtos, marcas e trocas são limitadas a 200 itens e indicam resultados adicionais para refinamento de filtros. Histórico oferece os 120 meses mais recentes com movimentos ou snapshots.

Manifest, ícones 192/512, ícone Apple, Service Worker, fallback offline e regra de navegação do Azure acompanham a SPA. Service Worker não armazena respostas de API, requisições autenticadas ou dados comerciais; somente recursos públicos explicitamente listados. A primeira versão exige conexão para consultar resultados. Instalação em aparelhos Android/iPhone físicos exige homologação posterior à publicação HTTPS.

## Verificação local

```powershell
dotnet test OroBI.slnx --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false
npm.cmd --prefix src/OroBI.Web test -- --run --maxWorkers=4
npm.cmd --prefix src/OroBI.Web run build
npm.cmd --prefix src/OroBI.Web run lint
node scripts/verify-seller-portal.mjs
```

O último comando usa Chromium local e somente fixtures fictícias em servidor local. Testes de API usam JWT real com EF InMemory. Geração de migration/SQL não substitui homologação relacional: nenhuma migration foi aplicada a PostgreSQL real durante esta implementação.

Resultado final em 2026-09-05, incluindo autocadastro: 316 testes .NET, 101 testes Web e 50 verificações no navegador passaram; build aprovado. Lint terminou sem erros, com três avisos em App.tsx. Consulte o [relatório de entrega](../audits/2026-09-05-seller-portal.md) e o [SQL idempotente das migrações](2026-09-05-seller-portal.sql) antes da ativação.
