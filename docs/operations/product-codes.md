# Códigos dos produtos no portal

O campo `CODPRODUTO` do POWER passa a ser armazenado como texto em `CommercialMovements.ProductCode`, preservando zeros à esquerda. A lista Produtos exibe `código · nome`. Códigos diferentes não são somados pelo simples fato de terem o mesmo nome; movimentos do mesmo código são agrupados, usando a descrição mais recente disponível. Registros antigos sem código continuam exibindo apenas o nome. O ranking de marcas permanece sem prefixo de produto.

## Ativação preparada

1. Aplicar a migração aditiva `20260905203406_AddCommercialProductCode` no banco BI. Ela cria uma coluna `text NOT NULL DEFAULT ''`, sem modificar valores comerciais. SQL idempotente preparado em `.worktrees/phase3-release-tools/product-code-migration.sql`.
2. Publicar a API e a interface com suporte ao novo campo. A API nova depende da migração aplicada; a interface aceita respostas antigas sem `productCode`.
3. Reenviar, pela importação administrativa existente, os mesmos arquivos POWER originalmente importados. O checksum deve corresponder ao lote anterior: não editar o CSV para tentar ativar esse caminho.
4. O caminho de reenvio preenche apenas códigos vazios, sem criar outro lote ou movimentos, sem sobrescrever códigos já existentes e sem alterar quantidades, valores, custos ou snapshots. Linhas são comparadas por todos os campos antigos; correspondências ambíguas permanecem sem código. O processo pode ser repetido.
5. Conferir um vendedor/cliente e a lista Produtos. Arquivos novos já armazenam o código na primeira importação; arquivos sem CODPRODUTO continuam compatíveis.

## Limites

Não se pode recuperar com certeza um código descartado se o arquivo original não estiver disponível ou se linhas com códigos diferentes se tornaram indistinguíveis nos campos gravados. Nesses casos o sistema não inventa associação. O código de produto não está presente no snapshot financeiro e sua inclusão não reabre fechamentos.

A preparação é local. Esta atualização e as correções anteriores da varredura ainda não foram aplicadas em produção.
