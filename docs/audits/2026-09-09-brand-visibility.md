# Visibilidade de marcas — 09/09/2026

A interface oculta marcas identificadas pelo nome como inativas (`INATIVO`, `INATIVA` e plurais, inclusive `ZZZ - INATIVO F`), sem informação (com ou sem acentos) ou vazias. A origem importada não oferece um campo de status específico para marcas. Marcas válidas continuam visíveis mesmo com valor negativo ou zero.

A regra comum é aplicada no portal de vendedores e gestores, nos gráficos e filtros do dashboard, nas análises de margem e troca, e nas metas e prêmios. Nos detalhes de vendas/clientes, somente o rótulo da marca é ocultado; o movimento e seus valores continuam aparecendo. As listas de produtos, clientes e demais dimensões mantêm seus rótulos. O filtro ocorre antes dos limites locais de ranking.

Nenhum registro, cálculo, total financeiro, percentual de participação ou resposta da API é alterado. Quando todas as marcas da lista são ocultadas, a tela apresenta seu estado vazio. Listas limitadas pelo servidor mostram a quantidade de marcas visíveis sem apresentar um total que inclua as ocultas.

Validação: 11 regressões nas telas afetadas e uma regressão adicional abrindo “Marcas” pela navegação do portal. As 11 regressões reproduziram o problema antes da correção e passaram depois. A suíte completa de interface passou inicialmente com 166 testes; os dois arquivos afetados pela inclusão final passaram com 25 testes. Build TypeScript/Vite concluído. Lint sem erros, com o aviso preexistente em `App.tsx`. Revisão independente sem defeitos acionáveis. A publicação usa o fluxo Web existente no plano Free, sem alterar a API ou utilizar credenciais administrativas.
