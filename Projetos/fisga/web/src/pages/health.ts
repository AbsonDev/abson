import type { APIRoute } from 'astro';

const version = process.env.GIT_SHA || process.env.SOURCE_COMMIT || 'dev';

export const GET: APIRoute = () =>
  new Response(JSON.stringify({ status: 'ok', version }), {
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store'
    }
  });
