# BI OROLEITE

BI administrativo e Portal do Vendedor na mesma aplicação: ASP.NET Core, Identity, PostgreSQL e React/TypeScript. O portal consulta os serviços comerciais existentes com autorização individual aplicada na API.

- [Guia do vendedor e da administração](docs/SELLER_PORTAL.md): cadastro, código ERP, primeiro acesso, recuperação, bloqueio e instalação no celular.
- [Autorização e sessão](docs/AUTHORIZATION.md): vínculos, permissões, endpoints pessoais e limites da sessão lembrada.
- [Auditoria do acesso mobile](docs/SELLER_PORTAL_AUDIT.md): diagnóstico inicial, alterações e evidências.
- [Operação da fase 3](docs/operations/seller-portal.md): importações, fechamento oficial, histórico e migrações anteriores.

O endereço Azure atual foi mantido. As melhorias de acesso mobile estão nesta branch e exigem a migração `20260906002605_AddSellerMobileAccess` antes de publicar a nova API e SPA. Nenhuma conta ou código ERP real é criado automaticamente.

## Verificação local

```powershell
dotnet test OroBI.slnx --configuration Release --disable-build-servers -m:1 /p:UseSharedCompilation=false
npm.cmd --prefix src/OroBI.Web ci
npm.cmd --prefix src/OroBI.Web test -- --run --maxWorkers=4
npm.cmd --prefix src/OroBI.Web run build
npm.cmd --prefix src/OroBI.Web run lint
```

A fonte comercial operacional continua sendo a importação CSV. Os contratos de sincronização Firebird existem, mas o leitor/worker operacional não foi entregue nesta tarefa. O navegador nunca consulta o ERP diretamente.
