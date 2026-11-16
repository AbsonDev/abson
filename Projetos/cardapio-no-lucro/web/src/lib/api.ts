export const apiBase = (
  process.env.API_URL ??
  import.meta.env.PUBLIC_API_URL ??
  'http://localhost:5182'
).replace(/\/$/, '');

export const apiPublica = (
  import.meta.env.PUBLIC_API_URL ?? 'http://localhost:5182'
).replace(/\/$/, '');

export async function obterApi<T>(caminho: string): Promise<T | null> {
  try {
    const resposta = await fetch(`${apiBase}${caminho}`);
    if (!resposta.ok) {
      return null;
    }

    return (await resposta.json()) as T;
  } catch {
    return null;
  }
}

export async function obterExemplo(): Promise<import('./tipos').ModeloDeFicha | null> {
  return await obterApi<import('./tipos').ModeloDeFicha>('/api/exemplo');
}

export async function obterCanais(): Promise<import('./tipos').ModeloDeCanal[]> {
  return (await obterApi<import('./tipos').ModeloDeCanal[]>('/api/canais')) ?? [];
}
