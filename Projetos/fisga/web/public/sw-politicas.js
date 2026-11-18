/**
 * Políticas de cache compartilhadas entre o service worker e os testes.
 *
 * `sw.js` carrega este arquivo com `importScripts`; o Playwright carrega o
 * mesmo arquivo com `addScriptTag` para testar a decisão sem depender de um
 * service worker ativo. Não use APIs exclusivas de service worker aqui.
 *
 * CHECKLIST: qualquer mudança neste arquivo exige bump de `VERSAO`/`sw.js`.
 * O registro usa `updateViaCache: 'none'`, mas o navegador só busca os imports
 * quando o próprio `sw.js` muda; sem o bump, a versão antiga continua ativa.
 */
(function (escopo) {
  // HTML que nunca pode ser gravado nem lido do cache. `/locais` é um link
  // antigo para o mapa e segue a mesma política do middleware.
  const ROTAS_PRIVADAS = [
    '/hoje',
    '/mapa',
    '/conta',
    '/meu-diario',
    '/registrar',
    '/trofeu',
    '/aceite',
    '/boas-vindas',
    '/locais'
  ];

  // Shell anônimo: muda com a sessão, então é aquecido sem cookie e só entra no
  // cache se a resposta se declarar pública.
  const PAGINAS_PUBLICAS = ['/', '/condicoes'];

  function combina(caminho, rota) {
    return caminho === rota || caminho.startsWith(`${rota}/`);
  }

  function rotaPrivada(caminho) {
    return ROTAS_PRIVADAS.some((rota) => combina(caminho, rota));
  }

  /** `/condicoes/` e `/` são as únicas páginas com cache de runtime. */
  function paginaPublica(caminho) {
    const limpo = caminho.length > 1 && caminho.endsWith('/') ? caminho.slice(0, -1) : caminho;
    return PAGINAS_PUBLICAS.includes(limpo);
  }

  /**
   * A decisão é pelo header, não só pelo path: `/condicoes` e `/` são públicos
   * para quem não tem sessão e privados para quem tem. `Set-Cookie` também
   * impede o cache, mesmo quando o `Cache-Control` não vem explícito.
   */
  function podeCachear(resposta) {
    if (!resposta || !resposta.ok) return false;

    const controle = (resposta.headers.get('Cache-Control') || '').toLowerCase();
    if (controle.includes('no-store') || controle.includes('private')) return false;
    if (resposta.headers.get('Set-Cookie')) return false;

    return true;
  }

  escopo.FisgaSWPoliticas = {
    ROTAS_PRIVADAS,
    PAGINAS_PUBLICAS,
    combina,
    rotaPrivada,
    paginaPublica,
    podeCachear
  };
})(typeof self !== 'undefined' ? self : globalThis);
