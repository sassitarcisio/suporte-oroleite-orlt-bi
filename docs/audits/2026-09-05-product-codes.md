# Código do produto antes do nome

Implementação local concluída em 05/09/2026. A lista Produtos apresenta `código · nome`, usando CODPRODUTO do POWER e preservando zeros à esquerda. Sem código disponível, apresenta apenas o nome. Produtos com códigos diferentes ficam separados mesmo quando possuem descrições iguais; movimentos do mesmo código usam a descrição mais recente. O escopo de vendedor continua aplicado antes do agrupamento.

O importador passa a persistir o campo. Reenviar exatamente o CSV original de um lote concluído preenche apenas códigos vazios com correspondência inequívoca, sem duplicar lotes ou movimentos, sobrescrever códigos ou alterar valores comerciais. A comparação considera a precisão decimal persistida no PostgreSQL; colisões e correspondências ambíguas permanecem sem código.

## Validação

- Suíte .NET: 369 testes aprovados, zero falhas (141 API, 78 Application e 150 Infrastructure). TRX em `.worktrees/phase3-release-tools/product-code-test-results`.
- Interface: 104 testes aprovados em 19 arquivos; build TypeScript/Vite concluído.
- Navegador local: 95 verificações aprovadas, nas larguras 360, 390, 430, 768 e 1440, com dados sintéticos. Evidência em [product-codes-ui-verification.json](evidence/product-codes-ui-verification.json).
- Revisão independente: problema de correspondência por precisão decimal corrigido e coberto por regressões; revisão final sem bloqueios.
- Migração aditiva gerada; modelo EF sem alterações pendentes. SQL idempotente preparado, não aplicado.

## Ativação pendente

A alteração ainda não está no portal oficial. A ativação exige aplicar a migração `20260905203406_AddCommercialProductCode`, publicar API/interface e reenviar os arquivos POWER originais para preencher códigos históricos. Ver [procedimento de ativação](../operations/product-codes.md).

As correções anteriores da varredura da fase 3 continuam presentes na mesma árvore de trabalho. Nenhuma publicação, migração de produção ou importação real foi realizada nesta tarefa. Os testes de recuperação usam banco em memória; a migração e a recuperação ainda não foram exercitadas no PostgreSQL de produção.
