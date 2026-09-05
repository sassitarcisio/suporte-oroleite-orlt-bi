# Detalhamento do fechamento

`GET /api/closings?seller=ANA&month=2026-08` mantém os campos anteriores e acrescenta:

- `monthly`: faturamento líquido dos movimentos (`revenue`), faturamento sem bonificações (`commissionableRevenue`), valor e percentual de troca, contagens de movimentos, clientes e documentos, escopo e documentos agrupados.
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
- `company-excluding-bauducco`: Valdir, empresa sem Operação Bauducco e sem bonificações.
- `company`: Deivid, empresa sem bonificações para o indicador de troca. Sua comissão continua combinando os escopos próprio, equipe e redes Bistek/Giassi.

Os fechamentos especiais mantêm seus prêmios próprios. Não retornam segmentos PPP ou metas por marca individuais; as listas ficam vazias. O campo legado `revenueAward` de Deivid continua contendo o prêmio da equipe e recebe esse nome na tela.

Nenhuma migração de banco é necessária. A tela usa o contrato expandido e deve ser distribuída junto com a API atualizada.

## Ordem de publicação

O workflow `deploy-web.yml` publica a interface automaticamente quando alterações em `src/OroBI.Web` chegam à branch `main`. A publicação da API é separada.

1. Publicar a API com o DTO expandido, mantendo a interface anterior em funcionamento.
2. Conferir uma consulta autenticada de fechamento e verificar `monthly`, `pppSegments`, `compensation.baseSalary`, `total` e os detalhes de `brandAwards`.
3. Enviar a alteração da interface para `main` e acompanhar o workflow de publicação. Conferir o fechamento padrão e os fechamentos especiais no ambiente publicado.

A interface expandida depende desses campos; publicar somente a interface antes da API pode interromper a consulta de fechamento.
