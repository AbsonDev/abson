/**
 * API mínima para os testes cujas páginas são renderizadas no servidor.
 *
 * `/hoje` chama `/api/auth/me` e `/p/[token]` chama `/api/pescarias/[token]`
 * fora do navegador, onde `page.route` não alcança. O cookie
 * `fisga_e2e_pendente=1` faz esta API responder uma sessão com aceite pendente.
 * Os demais testes continuam usando `page.route`; aqui só respondemos o que o
 * SSR precisa.
 */
import { createServer } from 'node:http';

const PORTA = 5184;
const COOKIE_PENDENTE = 'fisga_e2e_pendente=1';

const PESCARIA_NIVEIS_CIDADE = {
  id: 'pescaria-niveis-cidade',
  shareToken: 'niveis-cidade',
  entrada: {
    local: 'Campinas · SP',
    pescador: 'Marina Sá',
    cidade: 'Campinas',
    estado: 'SP',
    ambiente: 'Lagoa',
    data: '2026-11-19',
    periodo: 'Manha',
    mare: 'NaoSeAplica',
    lua: 'NaoInformada',
    clima: 'Sol',
    clarezaDaAgua: 'Clara',
    privacidadeDoLocal: 'Cidade',
    observacoes: null,
    capturas: []
  },
  capturas: [
    {
      id: 'captura-cidade',
      especie: 'Tilápia',
      pesoKg: 1.25,
      comprimentoCm: 34,
      isca: 'Massa',
      solto: false,
      fotoUrl: null,
      fotos: [],
      observacao: null
    }
  ],
  criadoEm: '2026-11-19T12:00:00Z',
  atualizadoEm: '2026-11-19T12:00:00Z',
  privacidadeDoLocal: 'Cidade',
  podeEditar: false
};

function json(resposta, status, corpo) {
  const texto = JSON.stringify(corpo);
  resposta.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(texto)
  });
  resposta.end(texto);
}

const servidor = createServer((requisicao, resposta) => {
  const url = new URL(requisicao.url ?? '/', 'http://localhost');
  const origem = requisicao.headers.origin;
  if (origem) {
    resposta.setHeader('access-control-allow-origin', origem);
    resposta.setHeader('access-control-allow-credentials', 'true');
  }

  if (requisicao.method === 'OPTIONS') {
    resposta.writeHead(204, {
      'access-control-allow-methods': 'GET,POST,OPTIONS',
      'access-control-allow-headers': 'content-type'
    });
    resposta.end();
    return;
  }

  if (url.pathname === '/__health') {
    json(resposta, 200, { ok: true });
    return;
  }

  if (url.pathname === '/api/pescarias/niveis-cidade') {
    json(resposta, 200, PESCARIA_NIVEIS_CIDADE);
    return;
  }

  if (url.pathname === '/api/auth/me') {
    const pendente = (requisicao.headers.cookie ?? '').includes(COOKIE_PENDENTE);
    json(resposta, 200, {
      autenticado: pendente,
      usuario: pendente
        ? {
            id: '1',
            email: 'pescador@fisga.app',
            nome: 'Marina Sá',
            nomeExibicao: 'Marina Sá',
            aceitaNovidades: false
          }
        : null,
      consentimentoPendente: pendente,
      googleConectado: false,
      temSenha: false
    });
    return;
  }

  json(resposta, 404, { codigo: 'nao_encontrado' });
});

servidor.listen(PORTA, () => {
  console.log(`mock-api ouvindo em http://localhost:${PORTA}`);
});
