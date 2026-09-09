# Login publicado no plano Free — 09/09/2026

A nova tela foi publicada em https://lively-sea-0776c9a0f.6.azurestaticapps.net. O recurso `orobi-web` permanece no plano **Free**, com uma função Node 22 gerenciada encaminhando `/api` para a API existente. Não houve upgrade, criação de recurso de computação separado, alteração do site legado `orlt-bi` ou de DNS. Os custos dos recursos existentes de API e banco continuam independentes do plano do site.

## Versão publicada

- Código: `4551ed003fe3743593ab8b6cf6939a3f7286c4fa`, integrado pelo [PR 7](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/pull/7) em `579fc8b1bb87f4f301204857616faf147e03c6c1`.
- [Publicação Web 34352695046](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/actions/runs/34352695046): sucesso em 09/09/2026 às 12:45 UTC, incluindo verificações HTTPS antes e depois da publicação.
- [CI da principal 34352694972](https://github.com/sassitarcisio/suporte-oroleite-orlt-bi/actions/runs/34352694972): sucesso. CI da branch e do PR também passaram.
- API: revisão `orobi-api--0000037`, tráfego de 100% para a revisão mais recente.
- Imagem: `orobiacr.azurecr.io/orobi-api@sha256:92b9ac781fa27f808887bc9a22289ae9c27d1e97cc1cf8d1e6d6da390c9e69ba`, build ACR `ch1h`.
- Imagem anterior para rollback: `orobiacr.azurecr.io/orobi-api@sha256:4cb5d20154a5814e150af94f3ae790568b18e8b0ed9535ac70194e2791cef02c` (revisão anterior `orobi-api--trades-943bca0`).

## Evidências e limites

Passaram 431 testes de backend, 155 de interface, 29 do gateway e 27 operacionais. O build passou; o lint mantém um aviso preexistente. A verificação local de navegador passou em 55 verificações, com seis tamanhos de tela: [relatório](login-browser-verification.md).

Em produção, a API e o gateway responderam `401` para identidade anônima, com `no-store`, e rejeitaram origem não autorizada com `403`. A publicação usa Functions Runtime 4 / Node 22. A conferência anônima do navegador passou em 15/15 verificações, com telas de celular e desktop revisadas e sem erros inesperados de console ou rede. A evidência está em [production-login-verification.json](login-evidence/production-login-verification.json).

O teste de login/logout com credencial real não foi executado: a revisão automática rejeitou a leitura e o uso da senha administrativa do Key Vault sem autorização específica. Nenhuma senha foi obtida e nenhum login foi tentado por esse procedimento. As propriedades do cookie, expiração e revogação passaram nos testes locais; a transmissão real de `Set-Cookie` pelo runtime gerenciado ainda depende desse teste autenticado ou de validação manual.

Instalação de PWA em aparelhos físicos Android/iOS não foi certificada. O gateway limita requisições a 30 MB pela plataforma e interrompe chamadas após 40 segundos; importações síncronas grandes precisam de validação própria. Nenhuma importação comercial foi feita para testar esta publicação.

Operação e rollback: [free-gateway.md](../operations/free-gateway.md).
