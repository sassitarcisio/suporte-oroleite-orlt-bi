# Código do produto antes do nome

Implementação local concluída em 05/09/2026. A lista Produtos apresenta `código · nome`, usando CODPRODUTO do POWER e preservando zeros à esquerda. Sem código disponível, apresenta apenas o nome. Produtos com códigos diferentes ficam separados mesmo quando possuem descrições iguais; movimentos do mesmo código usam a descrição mais recente. O escopo de vendedor continua aplicado antes do agrupamento.

O importador passa a persistir o campo. Reenviar exatamente o CSV original de um lote concluído preenche apenas códigos vazios com correspondência inequívoca, sem duplicar lotes ou movimentos, sobrescrever códigos ou alterar valores comerciais. A comparação considera a precisão decimal persistida no PostgreSQL; colisões e correspondências ambíguas permanecem sem código.

## Validação

- Suíte .NET: 369 testes aprovados, zero falhas (141 API, 78 Application e 150 Infrastructure). TRX em `.worktrees/phase3-release-tools/product-code-test-results`.
- Interface: 104 testes aprovados em 19 arquivos; build TypeScript/Vite concluído.
- Navegador local: 95 verificações aprovadas, nas larguras 360, 390, 430, 768 e 1440, com dados sintéticos. Evidência em [product-codes-ui-verification.json](evidence/product-codes-ui-verification.json).
- Revisão independente: problema de correspondência por precisão decimal corrigido e coberto por regressões; revisão final sem bloqueios.
- Migração aditiva gerada; modelo EF sem alterações pendentes. Aplicação em produção confirmada no job `orobi-migrate-bddh6b3`.

## Publicação e recuperação histórica

A publicação e a recuperação histórica foram aprovadas explicitamente pelo responsável e concluídas em 05/09/2026. O [PR #4](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/pull/4) integrou o código `fb208402c271687f98f0fa4350f5dbcc24276951` em `073cf5de91e2ee4650861a0c204ad8f0efe739b6`.

- Azure: imagem `orobiacr.azurecr.io/orobi-api:phase3-fb20840`, build ACR `ch1e`; migração `20260905203406_AddCommercialProductCode` concluída antes da API; revisão `orobi-api--phase3-fb20840` ativa e saudável.
- Interface: [publicação 33999127759](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/actions/runs/33999127759) e [CI 33999127741](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/actions/runs/33999127741) concluídos com sucesso.
- O POWER local era diferente do importado. O original foi recuperado do Blob privado e confirmado pelo checksum gravado no lote. Nenhum arquivo substituto foi importado.
- A recuperação executou o mesmo `CsvImportWorkflow` testado, com gravação de novos arquivos/lotes desabilitada no utilitário operacional. O lote mais recente entre cópias idênticas é o mesmo que o portal seleciona; cópias antigas permanecem preservadas e excluídas das consultas.
- Todos os movimentos com código no arquivo original receberam o código. Os movimentos restantes já tinham CODPRODUTO vazio na origem; a interface conserva apenas o nome nesses casos.
- Comparação integral de contagens e assinaturas dos dados antigos em nove tabelas confirmou a preservação de movimentos, lotes, erros, metas, PPP, configurações e snapshots. A coluna nova foi excluída da assinatura dos movimentos. Nenhum fechamento foi reaberto.
- API oficial: 14 verificações aprovadas, incluindo ranking de produtos, marcas, totais comerciais e bloqueio anônimo. Interface publicada: dez verificações aprovadas. [Evidência resumida sem dados comerciais](evidence/2026-09-05-product-codes-production.json).
- A regra temporária de acesso ao PostgreSQL, limitada ao IP da estação de recuperação, foi removida; a lista final contém somente a regra original.

As seis correções da [varredura da fase 3](2026-09-05-phase3-sweep.md) também foram publicadas. A verificação autenticada usou a conta administrativa para consultar um ranking autorizado; nenhuma conta de funcionário foi alterada ou personificada. Isso não substitui homologação com duas contas reais de vendedores nem os testes de concorrência de aprovação. Ver [procedimento de ativação](../operations/product-codes.md).
