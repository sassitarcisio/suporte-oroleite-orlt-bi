# Situação das correções da auditoria

As correções A01–A09 foram implementadas, revisadas e publicadas na API e na interface em 05/09/2026.

Validação: 223 testes backend, 71 testes Web e 21 testes operacionais aprovados; build aprovado; lint sem erros, com um aviso preexistente. CI e publicação Web concluídos com sucesso.

Verificações na aplicação publicada confirmaram os filtros aplicados, recuperação de sessão, gráficos e rankings. Nove páginas foram verificadas em 15 larguras, incluindo impressão. A conferência de leitura preservou os resultados comerciais de referência. Os arquivos detalhados dessa conferência permanecem locais e não acompanham este registro técnico.

Limites: o limitador de frequência de login atua por conta e por instância; o bloqueio Identity é persistente. Tokens existentes mantêm sua expiração. A equivalência de pesquisa foi verificada na base atual; outros locales do banco exigem validação própria.

A leitura integral do adendo da fase 2 foi concluída. A arquitetura API/PostgreSQL está operacional; leitor Firebird, Worker e homologação CSV × Firebird continuam pendentes, conforme [backlog](../TODO.md#fase-2---adendo).
