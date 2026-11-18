/**
 * Helpers de sessão para as telas públicas de autenticação (Task 11).
 *
 * A API responde em camelCase (System.Text.Json), mas o contrato C# usa
 * PascalCase. `obterSessao` aceita os dois para não depender da configuração
 * de serialização do host. Nenhuma função aqui registra e-mail, token ou
 * cookie em console/logs.
 */

export interface MeResponse {
  autenticado: boolean;
  usuario: {
    id: string;
    email: string;
    nome: string | null;
    nomeExibicao: string;
    aceitaNovidades?: boolean;
    criadoEm?: string;
    ultimoAcessoEm?: string | null;
  } | null;
  consentimentoPendente: boolean;
  googleConectado: boolean;
  temSenha: boolean;
}

export interface RespostaApi<T> {
  ok: boolean;
  status: number;
  dados: T | null;
  codigo?: string;
  mensagem?: string;
}

export function apiBase(): string {
  const base = typeof document !== 'undefined'
    ? document.body?.dataset.api || 'http://localhost:5184'
    : 'http://localhost:5184';
  return base.replace(/\/$/, '');
}

/**
 * Aceita apenas destinos internos. O `URL` normaliza `\\`, `//` e esquemas
 * (`/\evil.com` vira `http://evil.com/`), então a origem é comparada depois do
 * parse. Caracteres de controle são recusados antes, porque o parser os remove.
 */
export function destinoSeguro(raw: string | null | undefined, fallback = '/hoje'): string {
  if (!raw || typeof window === 'undefined') return fallback;
  if (/[\u0000-\u001F\u007F-\u009F]/.test(raw)) return fallback;
  try {
    const url = new URL(raw, window.location.origin);
    if (url.origin !== window.location.origin) return fallback;
    return `${url.pathname}${url.search}`;
  } catch {
    return fallback;
  }
}

function lerCampo(dados: Record<string, unknown>, camel: string, pascal: string): unknown {
  return dados[camel] ?? dados[pascal];
}

/** Lê `/api/auth/me` com cookie entre origens. Retorna null se não houver rede. */
export async function obterSessao(base: string = apiBase()): Promise<MeResponse | null> {
  try {
    const resposta = await fetch(`${base}/api/auth/me`, { credentials: 'include' });
    if (!resposta.ok) return null;
    const bruto = (await resposta.json()) as Record<string, unknown>;
    return {
      autenticado: Boolean(lerCampo(bruto, 'autenticado', 'Autenticado')),
      usuario: (lerCampo(bruto, 'usuario', 'Usuario') as MeResponse['usuario']) ?? null,
      consentimentoPendente: Boolean(
        lerCampo(bruto, 'consentimentoPendente', 'ConsentimentoPendente')
      ),
      googleConectado: Boolean(lerCampo(bruto, 'googleConectado', 'GoogleConectado')),
      temSenha: Boolean(lerCampo(bruto, 'temSenha', 'TemSenha'))
    };
  } catch {
    return null;
  }
}

/**
 * Se o usuário estiver autenticado com aceite pendente, redireciona para
 * `/aceite?destino=…` e retorna `true`. Chamada antes de carregar dados
 * protegidos, evitando os 403 do middleware de consentimento.
 */
export async function exigirConsentimento(
  destino?: string,
  base: string = apiBase()
): Promise<boolean> {
  const sessao = await obterSessao(base);
  if (!sessao?.consentimentoPendente || typeof window === 'undefined') return false;

  const alvo = destino ?? `${window.location.pathname}${window.location.search}`;
  window.location.replace(`/aceite?destino=${encodeURIComponent(alvo)}`);
  return true;
}

/** Chave do rascunho de pescaria; única exceção à limpeza de sessão anônima. */
export const CHAVE_RASCUNHO = 'fisga:rascunho:v1';

export interface OpcoesDeLimpeza {
  /**
   * Preserva o rascunho não salvo. Usado nas telas em que a sessão pode ter
   * expirado sem o pescador ter saído (ex.: `/registrar` e `/mapa` anônimos).
   * Logout explícito nunca usa esta opção.
   */
  preservarRascunho?: boolean;
}

/**
 * Apaga do aparelho o que é privado antes de encerrar a sessão: os caches
 * `fisga-private-*` (e versões antigas, anteriores à separação público/privado)
 * e as chaves `fisga:*` do localStorage (rascunho, locais, trilhas e pontos).
 * Com `preservarRascunho`, a única chave poupada é `fisga:rascunho:v1`.
 *
 * `fisga-public-*` e `fisga-tiles-v1` são preservados: só contêm o que passou em
 * `podeCachear` (wiki, ferramentas, offline e o shell anônimo de `/` e
 * `/condicoes`), então logout não pode quebrar o modo offline. Nunca rejeita:
 * Cache Storage ou localStorage indisponíveis não podem impedir o logout.
 * Outras chaves do mesmo domínio são preservadas.
 */
export async function limparCachePrivado(opcoes: OpcoesDeLimpeza = {}): Promise<void> {
  if (typeof window === 'undefined') return;

  try {
    if ('caches' in window) {
      const chaves = await caches.keys();
      await Promise.all(
        chaves
          .filter(
            (chave) =>
              chave.startsWith('fisga-')
              && !chave.startsWith('fisga-public-')
              && chave !== 'fisga-tiles-v1'
          )
          .map((chave) => caches.delete(chave))
      );
    }
  } catch {
    // Sem Cache Storage: segue para o localStorage.
  }

  try {
    const chaves: string[] = [];
    for (let indice = 0; indice < localStorage.length; indice += 1) {
      const chave = localStorage.key(indice);
      if (chave?.startsWith('fisga:')) chaves.push(chave);
    }
    for (const chave of chaves) {
      if (opcoes.preservarRascunho && chave === CHAVE_RASCUNHO) continue;
      localStorage.removeItem(chave);
    }
  } catch {
    // Sem localStorage (modo privado): não há o que limpar.
  }
}

let encerrandoSessaoExpirada = false;

/**
 * Encerra uma sessão expirada de forma consistente: apaga o estado privado do
 * aparelho e manda para `/entrar`, preservando o destino. É diferente do logout
 * explícito (que vai para `/entrar?saiu=1`), mas usa a mesma limpeza.
 *
 * O guard evita dois redirecionamentos quando a página faz mais de uma chamada
 * que descobre o 401 (ex.: carregamento inicial e uma ação do usuário).
 */
export async function encerrarSessaoExpirada(
  destino?: string,
  opcoes: OpcoesDeLimpeza = {}
): Promise<void> {
  if (typeof window === 'undefined' || encerrandoSessaoExpirada) return;
  encerrandoSessaoExpirada = true;

  const alvo = destino ?? `${window.location.pathname}${window.location.search}`;
  await limparCachePrivado(opcoes);
  window.location.replace(`/entrar?voltar=${encodeURIComponent(destinoSeguro(alvo))}`);
}

/** POST JSON com cookie de autenticação e leitura tolerante de problem+json. */
export async function postJson<T = Record<string, unknown>>(
  caminho: string,
  corpo: unknown,
  base: string = apiBase()
): Promise<RespostaApi<T>> {
  try {
    const resposta = await fetch(`${base}${caminho}`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo)
    });

    const texto = await resposta.text();
    let dados: Record<string, unknown> | null = null;
    if (texto) {
      try {
        dados = JSON.parse(texto) as Record<string, unknown>;
      } catch {
        dados = null;
      }
    }

    return {
      ok: resposta.ok,
      status: resposta.status,
      dados: dados as T | null,
      codigo: typeof dados?.codigo === 'string' ? dados.codigo : undefined,
      mensagem:
        typeof dados?.detalhe === 'string'
          ? dados.detalhe
          : typeof dados?.mensagem === 'string'
            ? dados.mensagem
            : undefined
    };
  } catch {
    return {
      ok: false,
      status: 0,
      dados: null,
      mensagem: 'Não foi possível falar com o Fisga agora. Verifique sua conexão e tente de novo.'
    };
  }
}
