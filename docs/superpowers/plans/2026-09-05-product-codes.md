# Código do produto no portal

**Objetivo:** mostrar `código · nome` em Produtos, usando CODPRODUTO do CSV, sem códigos inventados e preservando o escopo de vendedor.

**Arquitetura:** persistir ProductCode como texto (zeros à esquerda preservados); migração aditiva com vazio para históricos. DTO de ranking com ProductCode opcional; agrupamento de produtos por código quando disponível, nome como fallback apenas para registros sem código. Marcas conservam o contrato visual atual.

- [x] Acrescentar ProductCode à entidade e importar coluna opcional CODPRODUTO; manter CSVs antigos compatíveis. Gerar migração EF aditiva.
- [x] Ao reenviar exatamente o mesmo POWER já importado, preencher apenas códigos vazios de movimentos que tenham correspondência inequívoca com os dados da linha original. Não criar movimentos/lotes nem alterar números. Correspondências ambíguas ficam vazias.
- [x] Retornar ProductCode no ranking pessoal e agrupar códigos distintos separadamente, preservando totais/permissões.
- [x] Exibir código antes do nome em Produtos; registros sem código continuam com o nome.
- [x] Testar importação/reenvio, zeros à esquerda, agrupamento/escopo e apresentação; executar suites, build e navegador local.
- [x] Registrar ativação: migração, API/Web e reenvio do CSV original para preencher códigos históricos. Não publicar alterações locais da auditoria junto sem revisão/autorização.

Arquivos centrais: Domain/Commercial/CommercialMovement.cs; Infrastructure/Imports/CsvImportWorkflow.cs; Persistence/migrações; Application/Portal/PortalAnalytics.cs; Infrastructure/Portal/PortalQueryService.cs; Web/features/portal/PortalResults.tsx e portalTypes.ts. Preservar todos os ajustes locais anteriores da varredura.
