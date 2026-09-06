# Preparação da integração mobile

Escopo autorizado: preparar a branch `feature/seller-mobile-access` para integração, completando o CI e revalidando a entrega existente. Base remota consultada em 06/09/2026: `7b9443f8f86d72e15f3a28eb6f95f43e6c35629a`. Os commits `2c6e887` e `da0ba60` contêm acesso mobile e sessão corporativa.

## Alterações desta preparação

- O CI de push e pull request passa a executar testes Web, lint, os testes operacionais existentes e as sete verificações de configuração de cookies, além de .NET, build Web e Bicep.
- O job `operations` usa Windows Server 2025 e Pester 3.4.0, compatível com a sintaxe da suíte existente. Essa versão consta no [inventário oficial do runner](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md#powershell-modules).
- O check `verify` depende de `operations`, executa com `always()` e falha explicitamente se a dependência não terminar com sucesso. Isso evita o caso de [check obrigatório ignorado após falha de dependência](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks#handling-skipped-but-required-checks).
- O teste de implantação simula a consulta dos domínios da API, acrescentada pelo código mobile. Nenhuma chamada real ao Azure é necessária para os testes operacionais.
- A factory dos testes de sessão usa Data Protection com chaves efêmeras em memória. A geração e validação dos tokens de reset continuam reais, mas não dependem de gravar chaves no perfil Windows do desenvolvedor. A configuração de produção não foi alterada.

## Evidências locais de 06/09/2026

| Verificação | Resultado |
| --- | --- |
| .NET Release | 425 testes aprovados: 196 API, 78 Application e 151 Infrastructure; zero falhas. TRX local em `.worktrees/mobile-evidence/ci-test-results`. |
| Web completo, quatro workers | 150 testes aprovados em 28 arquivos. |
| Lint | Exit code 0; um aviso preexistente de `set-state-in-effect` em `App.tsx`. |
| TypeScript e Vite | Build de produção aprovado. |
| Bicep | `infra/main.bicep` compilado sem erros. |
| Pester 3.4.0 | 22 testes aprovados, zero falhas, incluindo a regressão do código de saída. |
| Configuração de cookies | Sete verificações aprovadas, Azure simulado. |
| Revisão independente | Sem bloqueios após corrigir a propagação da falha de `operations`; isolamento de Data Protection também revisado. |

Na primeira execução simultânea de build .NET e testes Web, dois testes Web atingiram o limite padrão de cinco segundos. Os dois arquivos passaram isoladamente (14 testes) e a suíte completa passou novamente com os mesmos quatro workers, sem mudar testes ou timeouts Web. A primeira suíte .NET teve cinco falhas em reset de senha, incluindo exceção explícita de acesso negado a `AppData/Local/ASP.NET/DataProtection-Keys`; isso motivou o isolamento das chaves na factory de teste.

Comandos de reprodução, executados a partir do worktree mobile:

```powershell
dotnet test OroBI.slnx --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false --logger trx --results-directory .worktrees/mobile-evidence/ci-test-results
npm.cmd --prefix src/OroBI.Web test -- --run --maxWorkers=4
npm.cmd --prefix src/OroBI.Web run lint
npm.cmd --prefix src/OroBI.Web run build
Import-Module Pester -RequiredVersion 3.4.0
$result = Invoke-Pester -Script tests/Operations -PassThru
if ($result.TotalCount -eq 0 -or $result.FailedCount -gt 0) { exit 1 }
./scripts/Test-DeployCookieConfiguration.ps1
```

No ambiente Windows local, os scripts operacionais foram executados em processo `powershell.exe -NoProfile -ExecutionPolicy Bypass`, sem mudar a política persistente do sistema. Bicep foi compilado com o executável local `bicep.exe build infra/main.bicep --outfile .worktrees/mobile-evidence/ci-main.json`; o CI mantém `az bicep build --file infra/main.bicep`.

## Preparação no GitHub

O envio ao repositório público foi autorizado explicitamente em 06/09/2026, após o bloqueio inicial da revisão automática. O [PR #5](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/pull/5) foi aberto em rascunho.

A [primeira execução do PR](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/actions/runs/34047918038) aprovou os testes operacionais, mas detectou que a última falha simulada do script de cookies deixava `LASTEXITCODE=1`, apesar das sete verificações aprovadas. O wrapper PowerShell do GitHub propaga esse valor ao processo. O script agora define zero somente depois que todas as verificações terminam; exceções inesperadas continuam a falhar. A regressão executa o script real e verifica o código observado pelo chamador: falhou antes da correção e passou depois. O wrapper equivalente ao Actions também terminou com exit code 0 localmente. O resultado remoto atualizado fica nos checks do PR.

## Limites da entrega

A preparação não aplica a migração `AddSellerMobileAccess`, não publica API/SPA e não altera DNS, certificados, contas reais ou o BI antigo. O `index.html` da raiz e os CSVs não fazem parte do diff da entrega mobile. Artefatos bin/obj e alterações anteriores do backlog permanecem fora do commit desta preparação.

Antes de integrar/publicar, seguir a ordem de compatibilidade do [procedimento de ativação](../operations/corporate-session-activation.md): migração aditiva e API compatível, SPA, DNS/HTTPS e ativação explícita do cookie. O merge em `main` dispara a publicação Web; o PR deve permanecer em rascunho até a coordenação dessas etapas. Homologação PostgreSQL real, aparelhos Android/iPhone físicos e aceite comercial continuam pendentes.
