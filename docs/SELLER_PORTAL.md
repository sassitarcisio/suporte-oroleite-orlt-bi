# Portal do Vendedor — acesso pelo celular

Guia das melhorias de acesso mobile de setembro de 2026. Esta entrega foi implementada e testada localmente; a ativação depende da publicação conjunta da API/SPA e da migração descrita abaixo.

## Endereço e identidade

O [endereço Azure do portal](https://lively-sea-0776c9a0f.6.azurestaticapps.net/portal) continua disponível. A arquitetura corporativa está preparada para `https://portal-bi.oroleite.com.br`, com API em `https://api-bi.oroleite.com.br`, após ativação de DNS e HTTPS. O BI antigo em `bi.oroleite.com.br` será mantido separado, conforme decisão do responsável. O login existente usa **e-mail e senha**. O mesmo acesso atende vendedores e administração.

Um vendedor tem UUID interno, nome de apresentação, nome correspondente ao arquivo comercial e, opcionalmente, **Código do vendedor no ERP** (`ExternalId`). Esse código é único quando preenchido, permite até 64 caracteres e é normalizado sem espaços nas pontas e em maiúsculas. Ele não substitui o UUID ou o nome importado. Não adivinhe o código pelo nome: confirme a correspondência com o cadastro de origem.

## Criar ou associar acesso

1. Entre como Administrador e abra **Portal do vendedor → Acessos**; no celular, use **Mais → Acessos**.
2. Em **Vendedores**, localize ou cadastre o vendedor. Preencha o nome importado correspondente aos movimentos; informe o código ERP conhecido. Para cadastros existentes, use **Editar código ERP → Salvar código ERP**.
3. Selecione o vendedor e use **Criar acesso para…**. O vínculo é pré-selecionado. Quando já houver conta de vendedor associada, a ação abre **Gerenciar acesso de…**, sem criar uma conta automaticamente.
4. Em **Contas e permissões**, informe nome da pessoa e e-mail, escolha **Vendedor** e confira exatamente um vínculo ativo com vendedor ativo. Defina os indicadores autorizados. Para associar uma conta existente, escolha-a em **Conta para editar** e salve seus vínculos.
5. Use **Gerar senha temporária e criar conta**. Também é possível informar uma senha inicial que cumpra a política. Ambas as formas exigem troca no próximo acesso.
6. A senha gerada pode ser revelada, copiada e descartada nessa tela. Ela não pode ser recuperada depois que a tela ou sessão for encerrada. Compartilhe-a diretamente com a pessoa identificada; o portal não envia mensagens ou e-mails automaticamente. Se perdida, gere outra.

O nome da pessoa é exibido no cadastro, mas sua edição após a criação não faz parte deste formulário. O último acesso registra somente logins bem-sucedidos; contas anteriores à atualização exibem **Nenhum acesso registrado** até um novo login. Não representa atividade contínua.

O **autocadastro continua disponível** em **Criar minha conta**. O usuário escolhe sua própria senha e fica pendente, sem acesso aos dados. O administrador confere nome/e-mail, associa um único vendedor e aprova. Essa senha escolhida pela pessoa não é tratada como temporária; um reset posterior volta a exigir troca.

## Primeiro acesso e senha

1. Abra o portal no celular e entre com e-mail e senha temporária.
2. A tela **Crie sua nova senha** aparece antes dos resultados. Informe senha atual, nova senha e confirmação.
3. Use pelo menos oito caracteres com maiúscula, minúscula, número e símbolo. A nova senha deve ser diferente da atual; o Identity valida a política no servidor.
4. Ao concluir, o aplicativo autentica com a nova senha mantida apenas em memória e abre o destino do perfil. Se a rede interromper essa etapa, entre novamente com a nova senha.

A exigência também é aplicada pela API: alterar URL, recarregar ou manipular o navegador não libera consultas com senha temporária.

Para alterar a senha voluntariamente, abra **Mais → Perfil**. O mesmo formulário é utilizado; ao terminar, entre novamente. **Esqueci minha senha** orienta a solicitar uma senha temporária ao administrador. Em **Acessos**, o administrador seleciona a conta e usa **Gerar nova senha temporária** ou **Redefinir senha**. O reset revoga sessões anteriores e exige troca.

## Sessão e bloqueio

No endereço corporativo configurado, o login usa cookie **HttpOnly, Secure e SameSite=Strict**, com validade absoluta máxima de oito horas e sem renovação automática. Fechar e reabrir a PWA no mesmo armazenamento preserva a sessão dentro desse prazo; o servidor valida a conta antes de mostrar dados. Nenhum token ou senha fica no armazenamento JavaScript nesse modo. Em dispositivo compartilhado, use **Sair**. Uma instalação pode ter armazenamento separado do navegador e exigir o primeiro login dentro da PWA.

No endereço Azure, o login oferece **Manter acesso neste dispositivo por até 8 horas**, desmarcado por padrão. Marque somente em dispositivo pessoal. A opção permite reabrir o portal no mesmo armazenamento do navegador/PWA enquanto o token estiver válido. Não salva a senha e não renova automaticamente o prazo. Uma instalação pode ter armazenamento separado do navegador; nesse caso, faça o primeiro login dentro da PWA.

Sem essa opção, o token fica em `sessionStorage`. Com ela, token e validade ficam também em `localStorage`; consulte as implicações em [Autorização](AUTHORIZATION.md). O servidor sempre confirma a identidade antes de mostrar resultados. Ao vencer o prazo, a tela limpa os resultados e pede novo login, inclusive ao voltar de uma aba suspensa.

**Sair** limpa os dados da tela e solicita revogação ao servidor. No modo corporativo, somente o servidor pode apagar o cookie HttpOnly; se a rede falhar, a tela informa que a sessão pode continuar no dispositivo e permite tentar novamente. A revogação existente usa o SecurityStamp e encerra também outras sessões da mesma conta. Sem conexão, o acesso local é removido, mas o aplicativo avisa que não conseguiu confirmar a revogação remota.

Em **Acessos**, use **Desativar conta** para impedir login e uso de sessões. **Desativar vendedor** impede o acesso individual vinculado e revoga sessões afetadas. Nenhuma dessas ações exclui movimentos, vendedores ou fechamentos históricos. Remover/alterar vínculos e permissões também revoga o acesso anterior.

## Resultados e navegação

No celular, os atalhos principais são **Início, Vendas, Metas, Clientes e Mais**, conforme permissões. Em **Mais** ficam os outros módulos e **Perfil**. A gestão pode escolher apenas vendedores autorizados; o vendedor individual não tem seletor de identidade.

A home apresenta metas, realizado e quanto falta usando os valores existentes da API: faturamento em reais e positivação em clientes. Metas de unidades distintas não são somadas. Comissão, prêmios e fechamento distinguem estimativas de valores oficiais aprovados; o snapshot aprovado continua imutável. O portal não exibe salário, custo, margem ou valores individuais de colegas.

O horário mostrado vem da origem dos dados. Quando a base contém apenas o horário de início de uma importação concluída, a tela informa **Importação iniciada em…**. Não usa a hora de abertura da página como atualização. O worker Firebird operacional continua pendente; não há sincronização fictícia.

## Instalar no Android ou iPhone

Em **Mais → Perfil → Instalar aplicativo**, consulte a orientação para seu aparelho. Quando o navegador oferecer o evento de instalação, aparece um botão para abrir a confirmação. A disponibilidade depende do navegador; o portal não promete instalação automática.

- **Android:** abra o endereço no Chrome e use o botão disponível ou o menu do navegador → **Instalar aplicativo/Adicionar à tela inicial**. Confirme e abra pelo ícone.
- **iPhone/iPad:** abra o endereço no Safari, toque em **Compartilhar → Adicionar à Tela de Início** e confirme. Os nomes podem variar conforme a versão.
- **Já instalado:** o aplicativo informa quando detecta o modo independente. O manifesto usa `/portal` como entrada e `display: standalone`.

É preciso conexão para consultar resultados. Sem rede, a PWA mostra a orientação de reconexão; APIs, comissões, senhas, prêmios e dados pessoais não entram no cache do service worker. O cache contém apenas arquivos públicos explicitamente permitidos.

## Publicação desta atualização

1. Revisar e aplicar `src/OroBI.Infrastructure/Persistence/Migrations/20260906002605_AddSellerMobileAccess.sql` pelo processo de migração do BI. O SQL é idempotente e contém somente esta migração, posterior a `AddCommercialProductCode`.
2. Publicar a API e a SPA compatíveis. A migração acrescenta `ExternalId`, `MustChangePassword`, `LastLoginAtUtc` e um índice único; preserva contas, vínculos, movimentos e snapshots existentes. Não preenche códigos ERP reais nem obriga contas antigas a trocar senha sem reset administrativo.
3. Manter `VITE_API_BASE_URL` para o endereço Azure e seguir [Ativação do domínio corporativo](operations/corporate-session-activation.md) para DNS, certificados e habilitação dos cookies. A migração de domínio não transfere sessões antigas; será necessário entrar novamente no novo endereço.
4. Homologar com duas contas de vendedores distintos, bloqueio/reset e instalação em Android/iPhone físicos no endereço HTTPS. A validação em Chrome local não certifica instalação física ou comportamento de armazenamento de todas as versões do Safari.

Resultados e limitações da entrega estão em [Auditoria](SELLER_PORTAL_AUDIT.md).
