# Metas e prêmios — apresentação

Implementado no checkout `.worktrees/login-free-release`, com base em `7d9e415`. A alteração está local; não foi publicada no Azure.

## Auditoria anterior à implementação

O componente `Goals` estava em `PortalResults.tsx`. `SellerPortal.tsx` controla o cabeçalho, o mês, as permissões e o carregamento do recurso. O contrato `PortalGoal` já fornece `target`, `actual`, `achievedPercent`, `currentPrize`, `nextTierPercent`, `amountToNextTier` e `nextTierPrize`.

`src/OroBI.Infrastructure/Portal/PortalQueryService.cs` calcula progresso, faltante e premiação. As faixas existentes são 80/90/100% para faturamento e 100% para positivação. O fechamento aprovado fornece os valores oficiais congelados. `src/OroBI.Api/Portal/PortalEndpoints.cs` aplica permissões e remove valores de prêmio quando o usuário não pode visualizá-los.

Nenhum desses arquivos, contrato ou formatador foi alterado. O diff do código fonte dos quatro projetos backend, excluindo artefatos bin/obj já existentes, está vazio em relação ao HEAD.

## Implementação

- `PortalGoals.tsx` concentra cards, progresso, faltante e premiação; `PortalResults.tsx` mantém a exportação existente.
- `PortalGoals.css` mantém a identidade visual, adiciona barras de 10px, callout de faltante e hierarquia tipográfica. Grid de uma coluna abaixo de 1100px e duas no desktop.
- `SellerPortal.tsx` recebe apenas o subtítulo da tela. O seletor de mês fica abaixo do título no celular.
- Status são exclusivamente visuais: sem percentual; em andamento; próximo da faixa quando faltam até 10 pontos percentuais para a próxima faixa informada; meta atingida em 100%; acima da meta após 100%.
- Somente a largura da barra é limitada a 0–100%; o percentual recebido continua no texto e na descrição acessível. Nenhum valor financeiro, quantidade faltante ou prêmio é recalculado.
- Prêmios estimados continuam identificados como estimativas. “Prêmio conquistado” é usado para valores positivos de um fechamento aprovado. Valores restritos permanecem ocultos.
- O resumo financeiro opcional não foi acrescentado: a API não fornece esse agregado, e o escopo preserva os cálculos existentes. O grid simples foi mantido.

## Comparação dos valores

| Indicador | Percentual exibido | Realizado / meta | Faltante | Próxima faixa | Prêmio atual | Próximo prêmio |
|---|---|---|---|---|---|---|
| Positivação | 26,67% | 4 / 15 clientes | 11 clientes | 100% | R$ 0,00 | R$ 100,00 |
| Faturamento | 22,05% | R$ 330,80 / R$ 1.500,00 | R$ 869,20 | 80% | R$ 0,00 | R$ 50,00 |

São os mesmos valores da imagem de referência. Os testes também verificam valores propositalmente diferentes de uma fórmula local (faltante R$ 812,34 e próximo prêmio R$ 47,89) e prêmio oficial congelado de R$ 91,23, garantindo a apresentação do dado recebido.

## Verificação

- Build: `npm.cmd run build` passou.
- Testes: `npm.cmd test -- --run --maxWorkers=2` passou, 181 testes em 31 arquivos. Inclui 14 testes novos dos cards, além das regressões do portal.
- Lint: `npm.cmd run lint` terminou com código 0, com um aviso existente em `src/App.tsx:449` sobre atualização de estado em efeito. Nenhum aviso nos arquivos desta alteração.
- Chrome: `node scripts/verify-goals-browser.mjs` passou com 78 verificações nas larguras 360, 390, 430, 768, 1366 e 1920px.
- Navegador verificou ausência de transbordamento horizontal, grid, espessura das barras, espaçamento, valores, porcentagens acessíveis, troca do mês, foco visível e contraste dos textos amostrados nos cards. Também verificou estados entre faixas, acima da meta, prêmio restrito, ausência de configuração e prêmio oficial.
- Revisão independente concluída sem pendências, após corrigir conflitos de especificidade com o CSS existente.

O navegador usou somente uma API local com dados sintéticos, reproduzindo os valores fornecidos pelo usuário. Não houve acesso a credenciais ou API de produção. A emulação de viewport não substitui teste em aparelhos físicos ou leitura com um leitor de tela real. Fontes externas opcionais foram bloqueadas no ensaio.

[Relatório detalhado](evidence/goals-ui/goals-ui-verification.json) · [Desktop 1366px](evidence/goals-ui/portal-ui-1366-goals.png) · [Celular 390px](evidence/goals-ui/portal-ui-390-goals.png) · [Faturamento em 360px](evidence/goals-ui/portal-ui-360-goals-revenue.png) · [Entre faixas](evidence/goals-ui/portal-ui-390-goals-edge-cases.png)
