# Business Rule Review

Source reviewed: legacy `index.html` dated 2026-09-02. This document records
observed behavior only; it does not approve or change commercial rules.

## Required source files

| File | Required fields / behavior |
| --- | --- |
| `POWER.csv` | `DATA`, `VENDEDOR`, `MARCA`, `TIPO`, `CIDADE`, `NOME`, `PRODUTO`, `VALTOTAL`, `QTDE`, `PRECOCUSTO`, `CODCLIENTE`, `NRODOCUMENTO`; the current legacy export provides `REDE`, which is mapped to the platform grouping field. |
| `ppp.csv` | `ANO`, `MES`, `VENDEDOR`, `SEGMENTO`, `QTDE_CLIENTES`, `QTDE_ITENS_SEGMENTO`, `GRUPOS_COLOCADOS`. |
| `metas.csv` | `VENDEDOR`, `MES`, `ANO`, `TIPOMETA`, `DESCRICAO`, `META`, `ALCANCADO`. |
| `VALOR_METAS.csv` | Reads base salary, commission percentage, PPP maximum, seller salaries and brand prizes/targets. |

## Confirmed rules

- POWER compatibility: `GRUPO` or `REDE` is accepted as the grouping source, with `GRUPO` taking precedence. For legacy `DESC BOLETO` rows only, blank `QTDE` and `PRECOCUSTO` are persisted as zero; other movement types still require valid numeric values.
- Trade view: only `TROCA` and `TROCA DEV`; values and quantities use absolute values. Trade percentage uses gross `VENDA` value.
- Sale x trade: revenue is the signed sum of `VENDA`, `DEVOL ENT`, and `DEVOLUCAO`; trade is the absolute sum of `TROCA` and `TROCA DEV`.
- Margin: only `VENDA`; cost is `QTDE * PRECOCUSTO`; gross profit is revenue minus cost; margin is profit divided by revenue.
- Standard seller commission: signed revenue excluding `BONIFICACAO` multiplied by the configured percentage.
- PPP segment rate: `GRUPOS_COLOCADOS / (QTDE_CLIENTES * QTDE_ITENS_SEGMENTO) * 100`; the award is PPP maximum multiplied by the mean of active segment rates.
- Goal prizes: positivacao pays 100% at 100%; faturamento pays 50% at 80%, 75% at 90%, and 100% at 100%; trade pays 100% when actual trade percentage is less than or equal to its target.

## Special closings

- Deivid Mannes: 1% on own revenue, 0.15% on the seven named team sellers, and 0.15% on Bistek/Giassi network revenue excluding Operacao Bauducco. Trade award bands are <=1.25%=5000, <=1.75%=3000, <=2.25%=2000, otherwise 0. The team incentive is the average of the seven sellers' incentives.
- Valdir Zacarias: Operacao Bauducco is excluded. Commission is 0.10% of signed company revenue. Trade award bands are <=2%=5000, <=3%=3000, <=4%=2000, otherwise 0.

## Required parity work

1. Capture approved CSV fixtures and expected values for every confirmed rule.
2. Add backend regression tests before changing any special-closing behavior.
3. Resolve whether signed values, duplicate network/team scope, and fallback values in `VALOR_METAS.csv` match the business-approved interpretation.
