# Ativação da sessão corporativa

**Atualização de 09/09/2026:** a implantação atual usa [gateway gerenciado no Free](free-gateway.md), com os hosts Azure existentes. O roteiro de domínios próprios abaixo permanece como alternativa histórica; não é requisito para essa publicação.

Implementação preparada localmente. O responsável confirmou acesso ao DNS e decidiu manter o BI antigo separado por enquanto. DNS, certificados, banco e produção não foram modificados nesta etapa.

## Endereços e registros

Consulta somente de leitura em 05/09/2026: bi.oroleite.com.br está no aplicativo orlt-bi, destino blue-island-06ce8b30f.7.azurestaticapps.net. Preservar esse vínculo. O portal atual está no aplicativo orobi-web. Os novos nomes portal-bi e api-bi retornaram inexistentes na consulta DNS. São configuráveis e devem ser revalidados antes da criação.

| Nome DNS | Tipo | Destino/valor |
| --- | --- | --- |
| portal-bi.oroleite.com.br | CNAME | lively-sea-0776c9a0f.6.azurestaticapps.net |
| api-bi.oroleite.com.br | CNAME | orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io |
| asuid.api-bi.oroleite.com.br | TXT | properties.customDomainVerificationId atual do Container App orobi-api |

Consultar o valor TXT e confirmar destinos atuais com leitura autenticada no Azure:

~~~powershell
az containerapp show --resource-group rg-oroleite-site --name orobi-api --query "{fqdn:properties.configuration.ingress.fqdn,verificationId:properties.customDomainVerificationId,domains:properties.configuration.ingress.customDomains}" --output json
az staticwebapp show --resource-group rg-oroleite-site --name orobi-web --query "{host:defaultHostname,domains:customDomains}" --output json
~~~

Não copiar valores de validação de outro aplicativo. Na consulta inicial a API não tinha domínio customizado e rejeitava HTTP inseguro; revalidar antes de ativar.

## Sequência de ativação

1. Revisar o pacote mobile/cookies e aplicar a migração aditiva AddSellerMobileAccess pelo processo existente. Cookies não acrescentam migração ou tabela. Publicar API compatível com BrowserSession desabilitado inicialmente; Azure segue com Bearer.
2. Criar os registros DNS. No Static Web App **orobi-web**, adicionar portal-bi.oroleite.com.br, concluir validação CNAME e emissão HTTPS. Preservar orlt-bi. Procedimento: [Static Web Apps com DNS externo](https://learn.microsoft.com/en-us/azure/static-web-apps/custom-domain-external).
3. No Container App **orobi-api**, adicionar api-bi.oroleite.com.br e vincular certificado gerenciado após CNAME/TXT propagados. Verificar certificado e cadeia HTTPS antes de habilitar cookies. Procedimento: [Container Apps e certificados gerenciados](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates).
4. Publicar SPA com VITE_COOKIE_PORTAL_ORIGIN=https://portal-bi.oroleite.com.br e VITE_COOKIE_API_BASE_URL=https://api-bi.oroleite.com.br. Manter VITE_API_BASE_URL da API Azure para o endereço legado. CSP já inclui a API corporativa; mudança futura exige atualizar connect-src e rebuild. Não distribuir o novo endereço antes de habilitar a API.
5. Revisar what-if de scripts/deploy-azure.ps1 com imagem exata da API, WebOrigin Azure existente, ConfigureRuntimeSecrets e EnableBrowserSession. Após autorização, usar Apply com os mesmos parâmetros. O script preserva TODOS os vínculos atuais de domínio/certificado e recusa ativação sem SniEnabled/certificateId no host da API. Falha de leitura interrompe antes do deployment. No workflow manual deploy-azure, marcar enable_browser_session ao ativar e nas implantações seguintes que devam manter cookies ligados; false desabilita.
6. Conferir BrowserSession__Enabled=true, BrowserSession__ApiHost=api-bi.oroleite.com.br, BrowserSession__AllowedOrigins__0=https://portal-bi.oroleite.com.br e allowInsecure=false. Verificar HTTPS nos dois hosts e CORS exato. Testar login, primeira senha, reabertura, logout e bloqueio com contas de homologação de vendedores distintos.
7. Homologar PWA instalada em Android e iPhone físicos. Abrir /portal, entrar, fechar e reabrir no mesmo contexto dentro do prazo absoluto; após expiração deve exigir login. PWA e navegador podem ter armazenamento separado. Sessões Azure não são transferidas. Divulgar somente após homologação.

Deploy direto de infra/main.bicep fora do script deve fornecer apiCustomDomains existentes: o padrão vazio serve ao provisionamento inicial e pode remover vínculos em infraestrutura já configurada. Usar o wrapper e revisar what-if.

## HTTPS entre ingresso e API

Container Apps termina TLS no ingresso gerenciado e encaminha o esquema original. Na ativação corporativa, Bicep configura ASPNETCORE_FORWARDEDHEADERS_ENABLED=true para o middleware automático reconhecer HTTPS. O contêiner fica atrás desse ingresso HTTP, sem portas TCP adicionais; a API exige Request.IsHttps, Host e Origin autorizados. Essa configuração pressupõe que somente o proxy controlado alcance a porta interna. Não reutilizar a confiança de cabeçalhos encaminhados em servidor exposto diretamente ou atrás de proxies arbitrários. Referências: [proxy ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) e [ingresso Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview).

## Política e retorno

Cookie __Host-OroBI.Session: Secure, HttpOnly, SameSite=Strict, Path=/, sem Domain; validade absoluta de até oito horas, sem renovação automática. Login JSON sem JWT, storage JavaScript sem credencial. Chamadas com credentials:include e X-OroBI-Session:cookie; API exige Origin e Host exatos, inclusive para GET/login/logout. O BI antigo não é origem permitida.

Sair revoga SecurityStamp e apaga cookie; falha de rede mostra aviso e permite tentar novamente. Cada chamada confere validade, conta/vendedor ativo, vínculos e troca obrigatória. Logout é global para a conta; se outra aba entrar ao mesmo tempo, pode ser necessário repetir o login. Em dispositivo compartilhado, usar Sair.

Para retorno operacional, desabilitar BrowserSession e direcionar ao endereço Azure existente. Preservar DNS e certificados. Cookie antigo deixa de autenticar enquanto o modo está desabilitado; não há transferência automática de sessão entre origens. A substituição futura de bi.oroleite.com.br exige plano para o aplicativo antigo e alinhamento das configurações Web/API.

## Evidências e limites

Testes locais verificam a API real com JWT/Identity e EF InMemory, além da UI em HTTPS no Chrome com servidor sintético. Não certificam DNS público, emissão real de certificado, PostgreSQL de produção ou instalação física iOS. Resultados em [Auditoria](../SELLER_PORTAL_AUDIT.md).
