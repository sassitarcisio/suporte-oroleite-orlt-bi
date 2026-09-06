# Sessão corporativa com cookie HttpOnly

A confirmação mais recente do responsável substitui a restrição anterior de manter apenas o domínio Azure: há acesso ao DNS oroleite.com.br e autorização para preparar HTTPS/cookies persistentes no projeto. Implementação local autorizada; nenhum DNS, certificado, conta ou implantação real será alterado nesta etapa.

## Decisão

O responsável confirmou que bi.oroleite.com.br continua no aplicativo antigo orlt-bi por enquanto. Usar portal-bi.oroleite.com.br para o portal atual orobi-web, preservando o vínculo antigo. A substituição futura terá ativação separada.

Manter SPA no Static Web Apps com `https://portal-bi.oroleite.com.br` e API no Container Apps com `https://api-bi.oroleite.com.br`. São origens distintas no mesmo site HTTPS. Isso preserva hospedagem e pipelines e exige dois nomes DNS. Uma origem única com proxy também seria possível, mas acrescentaria um serviço/roteamento; manter só a API Azure com o portal corporativo continuaria dependendo de cookies de terceiros. Não usar essas alternativas nesta entrega.

Cookie `__Host-OroBI.Session`, HttpOnly, Secure, SameSite=Strict, Path=/, sem Domain, persistente até a expiração absoluta já emitida (8h), sem renovação deslizante. Reutilizar JWT/Identity/SecurityStamp como conteúdo validado no servidor: não criar banco de senhas/sessões paralelo ou novas chaves de Data Protection. Nenhum JWT é retornado ao JavaScript no login cookie; frontend mantém apenas identificador local descartável de geração e validade, nunca credencial.

Login e requests cookie usam `X-OroBI-Session: cookie` e `credentials: include`. O backend exige configuração habilitada, Host da API permitido, Origin HTTPS exatamente permitido e esse cabeçalho para todas as requests cookie sob /api, inclusive login/logout e GET. Isso aplica o padrão OWASP de cabeçalho customizado + lista CORS exata; SameSite não é a única proteção CSRF. Sem Origin, Origin null, subdomínio não autorizado ou cabeçalho ausente =>403 sem efeitos. Bearer explícito permanece independente e não deve cair para cookie quando inválido. Preflight segue CORS; jamais usar wildcard com credenciais.

`BrowserSession:Enabled=false` por padrão; `ApiHost=api-bi.oroleite.com.br`; `AllowedOrigins=[https://portal-bi.oroleite.com.br]`. Login cookie solicita o modo pelo cabeçalho, não cria endpoint duplicado. Resposta contém `sessionMode:cookie`, `expiresAtUtc`, `roles`, `mustChangePassword`, sem accessToken. GET /me acrescenta validade do JWT assinado. Logout apaga cookie e revoga stamp; troca própria apaga cookie depois do sucesso/revoga e usa reautenticação existente. Reset/bloqueio/roles/vendedor inativo continuam validados por request. Respostas 401 não apagam cookie automaticamente para evitar resposta atrasada apagar uma sessão nova.

Web ativa cookies somente quando location.origin coincide com `VITE_COOKIE_PORTAL_ORIGIN` (padrão https://portal-bi.oroleite.com.br), usando `VITE_COOKIE_API_BASE_URL` (padrão https://api-bi.oroleite.com.br). Nas URLs Azure, preservar o Bearer existente. Bootstrap consulta /me com cookie antes de montar dados; sem sessão mostra login, com sessão valida flag/roles/prazo. Cookie corporativo sempre persiste por no máximo8h; checkbox Bearer não deve prometer controlar HttpOnly. Reabertura e troca de abas revalidam servidor; eventos locais não constituem autenticação. Storage antigo de Bearer é limpo no modo corporativo. Não armazenar cookie/token/senha em localStorage/sessionStorage; nenhum conteúdo privado no SW.

## Aceite e ativação

Testar emissão, expiração, reabertura, 401, revogação, primeira senha, CORS/CSRF com origens hostis e ausentes, Bearer legado e corridas entre sessões. Build/lint, documentação e smoke HTTPS em navegador local com fixtures sintéticas. DNS/CNAME/TXT e certificados devem ser provisionados e verificados antes de habilitar BrowserSession em produção; descrever os registros e processo sem inventar valores de validação Azure.

Referências consultadas: [OWASP CSRF](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html), [Static Web Apps domínio externo](https://learn.microsoft.com/en-us/azure/static-web-apps/custom-domain-external), [Container Apps domínio/certificado gerenciado](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates).
