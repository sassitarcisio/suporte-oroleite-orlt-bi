# Paridade CSV Legado

Os arquivos CSV de produção não devem ser versionados. Para executar a aceitação de paridade, disponibilize cópias autorizadas fora do repositório e registre aqui a evidência aprovada para cada corte:

| Corte | Período | Faturamento bruto | Negativos | Resultado líquido | Fonte da evidência |
| --- | --- | ---: | ---: | ---: | --- |
| Completo | A definir | A registrar | A registrar | A registrar | Exportação legada aprovada |
| Vendedor | A definir | A registrar | A registrar | A registrar | Exportação legada aprovada |
| Marca | A definir | A registrar | A registrar | A registrar | Exportação legada aprovada |
| Denominador zero | A definir | 0,00 | 0,00 | 0,00 | Cenário controlado |

Depois que os valores forem aprovados pelo negócio, transforme cada linha em uma fixture de teste de aplicação. Os testes devem comparar os resultados importados aos números desta evidência e nunca depender de um CSV local não versionado.
