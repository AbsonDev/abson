import type { APIRoute } from 'astro';

export const GET: APIRoute = ({ site }) => {
  const base = (import.meta.env.PUBLIC_SITE_URL ?? site?.origin ?? 'https://precifica.abson.tech').replace(/\/$/, '');
  const paginas = ['/', '/calculadora', '/precos'];

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
