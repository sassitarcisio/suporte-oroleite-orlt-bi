# Detalhamento do fechamento

`GET /api/closings?seller=ANA&month=2026-08` mantém os campos anteriores e acrescenta:

- `monthly`: faturamento líquido dos movimentos (`revenue`), base da comissão (`commissionableRevenue`), base de troca sem bonificações (`tradeRevenueBase`), valor e percentual de troca, contagens de movimentos, clientes e documentos, escopo e documentos agrupados.
- `compensation.baseSalary`: salário-base; `commission` e `totalSalary` continuam representando comissão e salário com comissão.
- `total`: salário com comissão mais `totalAwards`.
- `pppSegments`: segmento, clientes, itens por segmento, grupos colocados e percentual realizado.
- `brandAwards`: além dos prêmios apurados, retorna metas, realizado, percentual de atingimento e prêmio previsto de faturamento/positivação, valor de troca, taxa realizada, limite e prêmio previsto de troca.

## Bases e regras

Os dados mensais são filtrados por vendedor e período. `monthly.tradeValue` soma os valores absolutos de `TROCA` e `TROCA DEV`; `monthly.tradePercent` divide esse valor pelo módulo do faturamento sem bonificações e multiplica por 100. Base zero resulta em percentual zero, conforme a convenção existente dos cálculos.

Documentos são agrupados por número, data, vendedor, código do cliente e tipo de movimento. Os itens de cada grupo são somados. Números vazios não geram documentos, mas seus valores continuam compondo os indicadores. A quantidade de clientes usa códigos distintos e não vazios.

PPP calcula grupos colocados / (clientes × itens por segmento) × 100. Segmentos com clientes ou itens não positivos retornam percentual `null` e ficam fora da média, preservando a regra do prêmio PPP.

As metas e realizados por marca vêm de `GoalRecords`; os prêmios previstos e limites vêm do lote de `GoalValueRecords` usado no fechamento. A taxa de troca por marca preserva a base existente: todos os movimentos da marca, incluindo bonificações. Por isso pode diferir da taxa consolidada. A expansão não altera as regras de pagamento.

## Fechamentos especiais

`monthly.scope` identifica a abrangência:

- `seller`: vendedor selecionado.
- `company-excluding-bauducco`: Valdir, empresa sem Operação Bauducco. A comissão de 0,10% inclui bonificações; a base de troca as exclui. A comissão é arredondada para centavos antes de compor o total.
- `company`: Deivid, empresa sem bonificações para o indicador de troca. Sua comissão continua combinando os escopos próprio, equipe e redes Bistek/Giassi.

Os fechamentos especiais mantêm seus prêmios próprios. Não retornam segmentos PPP ou metas por marca individuais; as listas ficam vazias. O campo legado `revenueAward` de Deivid continua contendo o prêmio da equipe e recebe esse nome na tela.

Nenhuma migração de banco é necessária. A tela usa o contrato expandido e deve ser distribuída junto com a API atualizada.

## Importações repetidas e conferência de agosto de 2026

Dashboard e fechamento contam apenas o lote concluído mais recente de cada combinação de tipo de arquivo e checksum SHA-256. O histórico original permanece no banco. Linhas repetidas dentro de um mesmo arquivo são preservadas, pois podem representar itens legítimos. Arquivos com conteúdo diferente continuam sendo cargas independentes.

Reenviar um arquivo idêntico de movimentos, PPP ou metas já concluído retorna o resultado anterior, mesmo com outro nome, sem gravar outro arquivo ou acrescentar registros. Lotes concluídos com erros também são reconhecidos; lotes rejeitados não bloqueiam nova tentativa. `VALOR_METAS` mantém uma nova versão a cada envio para permitir restaurar configurações anteriores; o fechamento já utiliza somente sua versão mais recente. No PostgreSQL, um bloqueio transacional por conteúdo serializa envios simultâneos, inclusive entre réplicas da API.

O teste do demonstrativo oficial de Valdir reproduz quatro importações idênticas e confere: base da comissão R$ 4.557.465,78; base de troca R$ 4.546.665,61; trocas R$ 234.910,48 (5,17%); salário R$ 2.662,50; comissão R$ 4.557,47; prêmio zero; total R$ 7.219,97.

## Ordem de publicação

O workflow `deploy-web.yml` publica a interface automaticamente quando alterações em `src/OroBI.Web` chegam à branch `main`. A publicação da API é separada.

1. Publicar a API com o DTO expandido, mantendo a interface anterior em funcionamento.
2. Conferir uma consulta autenticada de fechamento e verificar `monthly`, `pppSegments`, `compensation.baseSalary`, `total` e os detalhes de `brandAwards`.
3. Enviar a alteração da interface para `main` e acompanhar o workflow de publicação. Conferir o fechamento padrão e os fechamentos especiais no ambiente publicado.

A interface expandida depende desses campos; publicar somente a interface antes da API pode interromper a consulta de fechamento.
