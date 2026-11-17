export const apiBase = (
  process.env.API_URL ??
  import.meta.env.PUBLIC_API_URL ??
  'http://localhost:5184'
).replace(/\/$/, '');

export const apiPublica = (
  import.meta.env.PUBLIC_API_URL ?? 'http://localhost:5184'
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

export async function obterCatalogo(): Promise<import('./tipos').Catalogo | null> {
  return await obterApi<import('./tipos').Catalogo>('/api/catalogo');
}
