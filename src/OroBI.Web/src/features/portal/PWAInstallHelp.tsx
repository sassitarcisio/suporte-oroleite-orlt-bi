export type PWAInstallation = { installed: boolean; canPrompt: boolean; busy: boolean; message: string; install: () => Promise<void> }

export default function PWAInstallHelp({ installation }: { installation: PWAInstallation }) {
  return <section className="portal-panel portal-install" aria-label="Instalação do aplicativo">
    <h2>Instalar no celular</h2>
    {installation.installed ? <p role="status">Aplicativo instalado neste dispositivo.</p> : <>
      <p className="portal-help">Adicione o portal à tela inicial para abri-lo como aplicativo. As opções dependem do navegador.</p>
      {installation.canPrompt && <button type="button" disabled={installation.busy} onClick={() => void installation.install()}>Instalar agora</button>}
      <p>Android: abra o menu do navegador e procure “Instalar aplicativo” ou “Adicionar à tela inicial”.</p>
      <p>iPhone: abra no Safari, toque em Compartilhar e escolha “Adicionar à Tela de Início”.</p>
      {installation.message && <p role="status">{installation.message}</p>}
    </>}
    <p className="portal-help">Você precisa de conexão para consultar seus resultados. A instalação não libera acesso offline aos dados comerciais.</p>
  </section>
}
