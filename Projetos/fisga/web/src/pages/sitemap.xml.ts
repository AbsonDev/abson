import type { APIRoute } from 'astro';
import { getCollection } from 'astro:content';

export const GET: APIRoute = async ({ site }) => {
  const base = (import.meta.env.PUBLIC_SITE_URL ?? site?.origin ?? 'https://fisga.abson.tech').replace(/\/$/, '');

  const fixas = [
    '/',
    '/precos',
    '/registrar',
    '/condicoes',
    '/wiki',
    '/wiki/especies',
    '/wiki/tecnicas',
    '/wiki/iscas',
    '/wiki/locais',
    '/wiki/defeso',
    '/wiki/iniciantes',
    '/wiki/seguranca',
    '/wiki/glossario',
    '/wiki/fontes',
    '/sobre',
    '/contato',
    '/privacidade',
    '/termos'
  ];

  const colecoes = ['especies', 'tecnicas', 'iscas', 'locais'] as const;
  const artigos: string[] = [];

  for (const colecao of colecoes) {
    const entradas = await getCollection(colecao);
    for (const entrada of entradas) {
      artigos.push(`/wiki/${colecao}/${entrada.id}`);
    }
  }

  const paginas = [...fixas, ...artigos];

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${paginas.map((pagina) => `  <url><loc>${base}${pagina}</loc></url>`).join('\n')}
</urlset>`;

  return new Response(xml, {
    headers: {
      'Content-Type': 'application/xml; charset=utf-8'
    }
  });
};
