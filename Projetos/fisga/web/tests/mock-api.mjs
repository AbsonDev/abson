/**
 * API mínima para o teste de gate de consentimento de `/hoje`.
 *
 * `/hoje` é renderizado no servidor e chama `/api/auth/me` fora do navegador,
 * onde `page.route` não alcança. O cookie `fisga_e2e_pendente=1` faz esta API
 * responder uma sessão com aceite pendente. Os demais testes continuam usando
 * `page.route`; aqui só respondemos o que o SSR precisa.
 */
import { createServer } from 'node:http';

const PORTA = 5184;
const COOKIE_PENDENTE = 'fisga_e2e_pendente=1';

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
      googleConectado: false
    });
    return;
  }

  json(resposta, 404, { codigo: 'nao_encontrado' });
});

servidor.listen(PORTA, () => {
  console.log(`mock-api ouvindo em http://localhost:${PORTA}`);
});
