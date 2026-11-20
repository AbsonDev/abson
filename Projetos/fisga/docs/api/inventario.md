# Inventário da API do Fisga

Matriz de cobertura da API usada como insumo da documentação pública
(issue #58). Uma linha por operação registrada no contrato: o que ela faz, quem
pode chamar, como autentica, qual limite e cache se aplicam, qual resposta de
sucesso o código devolve de verdade e quais erros precisam ser documentados.

**Estado:** Fase 0 concluída em 2026-11-20 · **Contrato de referência:**
[`openapi.json`](openapi.json) (OpenAPI 3.1.1, 54 operações em 48 rotas).

## Como manter

1. Mudou rota, acesso, status ou erro? Atualize a linha e o `openapi.json` no
   mesmo PR.
2. O `openapi.json` é gerado por `scripts/gerar-openapi.sh`; este inventário é
   escrito à mão porque cobre comportamento que o gerador ainda não expressa
   (acesso, rate limit, cache, semântica de erro).
3. A coluna "Sucesso real" reflete o código, não o que o spec declara hoje. As
   divergências estão na seção [Lacunas de contrato](#lacunas-de-contrato).

## Resumo por domínio

| Domínio | Operações | Público (inclui token) | Sessão/dono | Admin/token |
| --- | --- | --- | --- | --- |
| Sistema & Legal | 3 | 3 | — | — |
| Catálogo | 1 | 1 | — | — |
| Autenticação & Sessão | 16 | 12 | 4 | — |
| Conta & Consentimento | 5 | — | 5 | — |
| Pescarias & Compartilhamento | 6 | 3 | 3 | — |
| Diário | 1 | — | 1 | — |
| Condições | 2 | 1 | 1 | — |
| Locais | 7 | 2 | 5 | — |
| Pontos | 4 | — | 4 | — |
| Trilhas | 4 | — | 4 | — |
| Mídia | 1 | — | 1 | — |
| Visão/Espécies | 1 | — | 1 | — |
| Newsletter/Admin | 3 | 1 | — | 2 |
| **Total** | **54** | **23** | **29** | **2** |

Métodos: 27 `GET`, 18 `POST`, 4 `PUT`, 4 `DELETE`, 1 `PATCH`. "Público" inclui
leitura por capacidade (`shareToken` da pescaria e token assinado do
descadastro), que não exige conta.

## Regras transversais

### Autenticação e sessão

- **Cookie:** `fisga_auth` (ASP.NET Core Identity), `HttpOnly`, `SameSite=Lax`,
  `Secure` em produção, expiração deslizante de 30 dias.
- **Sessão de dispositivo:** cada requisição `/api` valida o claim `sessao_id`
  contra a tabela de sessões. Sessão revogada, expirada ou de outro usuário
  invalida o cookie na hora.
- **Ausência de sessão:** endpoints que chamam `ExigirUsuarioId()` respondem
  `401` com `{"codigo":"nao_autenticado"}`. Endpoints anônimos não falham.
- **Consentimento:** usuário autenticado com termos/privacidade pendentes recebe
  `403` `consentimento_pendente` em qualquer rota `/api`, exceto
  `GET /api/auth/me`, `POST /api/auth/sair` e `POST /api/conta/consentimento`.
- **CSRF:** mutações (`POST`, `PUT`, `PATCH`, `DELETE`) em `/api` com header
  `Origin` de outro site recebem `403` `origem_invalida`. CORS é restrito a
  `Cors:AllowedOrigins` e o cookie é `SameSite=Lax`.
- **Admin:** `POST /api/newsletter/enviar` e `GET /api/newsletter/preview` usam
  o header `X-Admin-Token` comparado em tempo constante. Token ausente ou
  inválido responde `404` de propósito, para não revelar a existência da rota.
- **Token assinado:** `GET /api/newsletter/descadastrar` valida um token na
  query; `shareToken` em `/api/pescarias/{shareToken}` é a capacidade de leitura
  pública de uma pescaria.

### Rate limit

Janela fixa de 1 minuto por IP (`FixedWindowLimiter`), configurável por
`RateLimiting:<política>`:

| Política | Limite padrão | Onde se aplica |
| --- | --- | --- |
| `leitura` | 180/min | Consultas públicas e autenticadas de leitura |
| `escrita` | 40/min | Criação, edição, exclusão e conta |
| `auth` | 8/min | Login, cadastro, recuperação de senha, Google e descadastro |
| `pdf` | 30/min | Geração de PDF da pescaria |
| `midia` | 30/min | Upload de foto e sugestão de espécie |

`/health`, `/ready` e as duas rotas administrativas de newsletter não têm
política de rate limit hoje. A rejeição é `429` **sem corpo** e sem
`Retry-After`.

### Cache

Default-deny: sem metadado `CachePolicy`, a resposta sai com
`Cache-Control: private, no-store`. Erros (`>= 400`) sempre saem como
`private, no-store`.

| Endpoint | Cache-Control |
| --- | --- |
| `GET /api/legal/versoes` | `public, max-age=86400` |
| `GET /api/catalogo` | `public, max-age=86400` |
| `GET /api/pescarias/{shareToken}` | `private, max-age=30` |
| `GET /api/pescarias/{shareToken}/pdf` | `private, max-age=300` |
| `GET /api/condicoes` | `private, max-age=300, stale-while-revalidate=600` |
| `GET /api/locais/reverso` | `private, max-age=86400` |
| `GET /api/locais/buscar` | `private, max-age=3600` |
| Demais rotas | `private, no-store` |

### Formato de erro

Exceções de domínio e de acesso são convertidas pelo
[`ExceptionHandlingMiddleware`](../../src/Fisga.Api/Middleware/ExceptionHandlingMiddleware.cs)
em `application/problem+json` com dois campos:

```json
{ "codigo": "pescaria_nao_encontrada", "detalhe": "Pescaria não encontrada." }
```

| Origem | Status | `codigo` |
| --- | --- | --- |
| Regra de negócio (`DomainException`) | 400 | valor da exceção (ex.: `confirmacao_invalida`) |
| Sem sessão (`NaoAutenticadoException`) | 401 | `nao_autenticado` |
| Recurso inexistente | 404 | valor da exceção (ex.: `pescaria_nao_encontrada`) |
| Sem permissão | 403 | `acesso_negado` |
| Consentimento pendente | 403 | `consentimento_pendente` |
| `Origin` não permitida | 403 | `origem_invalida` |
| Exceção não tratada | 500 | `erro_inesperado` |
| Rate limit | 429 | — (corpo vazio) |
| Falha de binding do Minimal API | 400 | ProblemDetails padrão do ASP.NET Core |

## Domínios

Legenda de acesso: **P** público · **S** sessão · **D** dono · **A** admin ·
**T** token assinado/`shareToken`. "Consent." indica que o bloqueio de
consentimento pendente se aplica.

### Sistema & Legal

| Método e rota | Acesso | Rate limit | Cache | Sucesso real | Erros a documentar |
| --- | --- | --- | --- | --- | --- |
| `GET /health` | P | — | no-store | `200` `{status, version}` | `500` |
| `GET /ready` | P | — | no-store | `200` `{status:"ready"}` | `503` (banco inacessível), `500` |
| `GET /api/legal/versoes` | P | leitura | public 1 dia | `200` `VersoesLegaisResponse` | `500` |

`/health` é liveness (não toca o banco). `/ready` executa `CanConnectAsync` e
responde `503` quando o banco não responde.

### Catálogo

| Método e rota | Acesso | Rate limit | Cache | Sucesso real | Erros a documentar |
| --- | --- | --- | --- | --- | --- |
| `GET /api/catalogo` | P | leitura | public 1 dia | `200` `CatalogoDto` | `500` |

### Autenticação & Sessão

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- |
| `POST /api/auth/solicitar` | P | auth | `202 Accepted` + `SolicitarLinkResponse` | `400`, `429` | Resposta genérica anti-enumeração; `link` só aparece em Development |
| `POST /api/auth/registrar` | P | auth | `201 Created` + `RegistrarResponse` | `400`, `429` | Cria conta e envia confirmação de e-mail |
| `POST /api/auth/entrar` | P | auth | `200` `EntrarResponse` | `400`, `429` | Falha de credencial, lockout e e-mail não confirmado também são `200` com `autenticado:false` |
| `POST /api/auth/confirmar-email` | P | auth | `200` `ConfirmarEmailResponse` | `400` `confirmacao_invalida`, `429` | |
| `POST /api/auth/esqueci-senha` | P | auth | `202 Accepted` + `EsqueciSenhaResponse` | `400`, `429` | Resposta genérica anti-enumeração |
| `POST /api/auth/redefinir-senha` | P | auth | `200` `RedefinirSenhaResponse` | `400` `link_invalido`/`senha_curta`/`senha_longa`, `429` | Revoga todas as sessões e rotaciona o security stamp |
| `GET /api/auth/confirmar` | P | auth | `302` para o web | `400`, `302` com `estado=<codigo>` | Link mágico; emite o cookie de sessão |
| `GET /api/auth/google/disponivel` | P | auth | `200` `GoogleDisponivelResponse` | `429` | |
| `GET /api/auth/google/iniciar` | P | auth | `302` challenge Google | `503` `google_indisponivel`, `429` | |
| `GET /api/auth/google/concluir` | P | auth | `302` para o web | `302` com `estado=<codigo>`, `429` | Callback OAuth; primeiro acesso vai para `/boas-vindas` |
| `GET /api/auth/google/conectar` | S | auth | `302` challenge Google | `401`, `503` `google_indisponivel`, `429` | Inicia vínculo de Google à conta logada |
| `GET /api/auth/google/conectar/concluir` | S (cookie de intenção) | auth | `302` para `/conta?estado=google_conectado` | `302` com `estado=<codigo>`, `429` | Sem sessão ou intenção inválida também redireciona |
| `GET /api/auth/me` | P | leitura | `200` `MeResponse` | `429` | **Nunca responde 401**: sem sessão devolve `autenticado:false` |
| `PATCH /api/auth/perfil` | S, consent. | escrita | `200` `UsuarioResponse` | `400`, `401`, `403`, `429` | |
| `POST /api/auth/sair` | P | escrita | `204 No Content` | `429` | Idempotente: sem sessão também responde `204` |
| `POST /api/auth/sair-de-todos` | S, consent. | escrita | `204 No Content` | `401`, `403`, `429` | Revoga sessões e rotaciona o security stamp |

### Conta & Consentimento

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- |
| `POST /api/conta/consentimento` | S (allowlist) | escrita | `204 No Content` | `400` `consentimento_versao_invalida`, `401`, `429` | Único endpoint de consentimento liberado com pendência |
| `POST /api/conta/senha` | S, consent. | escrita | `204 No Content` | `400` `senha_atual_incorreta`/`senha_curta`/`senha_longa`, `401`, `403`, `429` | Revoga as outras sessões e reemite o cookie da sessão atual |
| `GET /api/conta/sessoes` | S, consent. | leitura | `200` `SessaoResponse[]` | `401`, `403`, `429` | |
| `GET /api/conta/exportar` | S, consent. | leitura | `200` `application/json` com `Content-Disposition` | `401`, `403`, `429` | Download `fisga-conta-AAAAAMDD.json` |
| `DELETE /api/conta` | S, consent. | escrita | `204 No Content` | `400`, `401`, `403`, `429` | Corpo exige confirmação; encerra a sessão |

### Pescarias & Compartilhamento

| Método e rota | Acesso | Rate limit | Cache | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- | --- |
| `POST /api/pescarias` | S, consent. | escrita | no-store | `201 Created` + `PescariaSalvaResponse` | `400`, `401`, `403`, `429` | `Location` aponta para a pescaria criada |
| `GET /api/pescarias/{shareToken}` | P/T | leitura | private 30 s | `200` `PescariaDto` | `404` `pescaria_nao_encontrada` | Visão pública; com sessão do dono inclui `podeEditar` |
| `GET /api/pescarias/{shareToken}/permissao` | P/T | leitura | no-store | `200` `PermissaoResponse` | `404` `pescaria_nao_encontrada` | |
| `GET /api/pescarias/{id}/editar` | S/D | leitura | no-store | `200` `PescariaDto` | `401`, `403`, `404` | |
| `PUT /api/pescarias/{id}` | S/D, consent. | escrita | no-store | `200` `PescariaDto` | `400`, `401`, `403`, `404`, `429` | |
| `GET /api/pescarias/{shareToken}/pdf` | P/T | pdf | private 5 min | `200` `application/pdf` | `404`, `429` | Resposta binária com nome de arquivo |

### Diário

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar |
| --- | --- | --- | --- | --- |
| `GET /api/diario` | S/D, consent. | leitura | `200` `DiarioDto` | `401`, `403`, `429` |

### Condições

| Método e rota | Acesso | Rate limit | Cache | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- | --- |
| `GET /api/condicoes` | P | leitura | private 5 min + SWR 10 min | `200` `CondicoesDto` | `400` `local_sem_coordenadas`, `429`, `500` | `lat` e `lon` obrigatórios; `ambiente` inválido cai em `Outro` |
| `GET /api/condicoes/comparar` | S, consent. | leitura | no-store | `200` `ComparacaoDto` | `400`, `401`, `403`, `500`, `429` | Usa o histórico do usuário |

### Locais

| Método e rota | Acesso | Rate limit | Cache | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- | --- |
| `GET /api/locais/reverso` | P | leitura | private 1 dia | `200` resultado do geocodificador | `400` `coordenadas_invalidas`, `404`, `429` | |
| `GET /api/locais/buscar` | P | leitura | private 1 h | `200` lista de locais | `429` | Query `q` vazia devolve lista vazia |
| `GET /api/locais` | S, consent. | leitura | no-store | `200` lista de locais salvos | `401`, `403`, `429` | |
| `POST /api/locais` | S, consent. | escrita | no-store | `201 Created` + local | `400`, `401`, `403`, `429` | |
| `PUT /api/locais/{id}` | S/D, consent. | escrita | no-store | `200` local | `400`, `401`, `403`, `404`, `429` | |
| `POST /api/locais/{id}/padrao` | S/D, consent. | escrita | no-store | `200` local | `401`, `403`, `404`, `429` | Define o local padrão do usuário |
| `DELETE /api/locais/{id}` | S/D, consent. | escrita | no-store | `204 No Content` | `401`, `403`, `404`, `429` | |

### Pontos

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar |
| --- | --- | --- | --- | --- |
| `GET /api/pontos` | S, consent. | leitura | `200` `PontoDto[]` | `401`, `403`, `429` |
| `POST /api/pontos` | S, consent. | escrita | `201 Created` + `PontoDto` | `400`, `401`, `403`, `429` |
| `PUT /api/pontos/{id}` | S/D, consent. | escrita | `200` `PontoDto` | `400`, `401`, `403`, `404`, `429` |
| `DELETE /api/pontos/{id}` | S/D, consent. | escrita | `204 No Content` | `401`, `403`, `404`, `429` |

### Trilhas

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar |
| --- | --- | --- | --- | --- |
| `GET /api/trilhas` | S, consent. | leitura | `200` `TrilhaDto[]` | `401`, `403`, `429` |
| `POST /api/trilhas` | S, consent. | escrita | `201 Created` + `TrilhaDto` | `400`, `401`, `403`, `429` |
| `PUT /api/trilhas/{id}` | S/D, consent. | escrita | `200` `TrilhaDto` | `400`, `401`, `403`, `404`, `429` |
| `DELETE /api/trilhas/{id}` | S/D, consent. | escrita | `204 No Content` | `401`, `403`, `404`, `429` |

### Mídia

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- |
| `POST /api/midia` | S, consent. | midia | `200` `MidiaResponse` | `400` `midia_sem_formulario`/`midia_vazia`/`midia_grande`/`midia_tipo_invalido`, `401`, `403`, `413` (Kestrel), `429` | `multipart/form-data`, campo `arquivo`; JPG, PNG ou WebP até 5 MB |

### Visão/Espécies

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- |
| `POST /api/especies/sugerir` | S, consent. | midia | `200` resultado do reconhecedor | `400` `sugestao_sem_formulario`/`sugestao_sem_foto`/`sugestao_foto_grande`/`sugestao_tipo_invalido`, `401`, `403`, `429`, `500` | `multipart/form-data`, campo `arquivo`; até 5 MB |

### Newsletter/Admin

| Método e rota | Acesso | Rate limit | Sucesso real | Erros a documentar | Notas |
| --- | --- | --- | --- | --- | --- |
| `POST /api/newsletter/enviar` | A | — | `200` resultado do envio | `404` (token inválido), `500` | Query `mes`, `dryRun=1`; `X-Admin-Token` obrigatório |
| `GET /api/newsletter/preview` | A | — | `200` `text/html` | `404` (token inválido ou e-mail sem mensagem), `500` | Query `email`, `mes`; `X-Admin-Token` obrigatório |
| `GET /api/newsletter/descadastrar` | T | auth | `302` para `/newsletter/descadastrado?ok=0\|1` | `302` com `ok=0`, `429` | Token assinado na query |

## Lacunas de contrato

Confirmadas no `openapi.json` atual. Viram tickets da Fase 1.

| # | Lacuna | Evidência | Correção alvo |
| --- | --- | --- | --- |
| 1 | Spec declara apenas `200` em operações que devolvem `201`, `202` ou `204` | `POST /api/pescarias`, `/api/locais`, `/api/pontos`, `/api/trilhas` (201); `/api/auth/solicitar`, `/api/auth/esqueci-senha` (202); `DELETE`s, `sair`, `sair-de-todos`, consentimento e senha (204) | `Produces<T>` nos handlers e `ProducesProblem` nos erros |
| 2 | Nenhum `securityScheme` declarado | Cookie `fisga_auth` vive no middleware; `X-Admin-Token` e `shareToken` só existem no código | Declarar `cookieAuth`, `adminToken` e `shareToken`; exigir por operação |
| 3 | Formato de erro real não é RFC 9457 | `ErroResponse` tem `{codigo, detalhe}` | Documentar o contrato real e decidir se migra para ProblemDetails |
| 4 | Erros vêm de origens diferentes e nem todos têm corpo | Binding, middlewares, rate limiter e handlers | Catálogo de `codigo` + schema único de erro |
| 5 | Rate limit e cache não aparecem no contrato | `AddRateLimiter` e `CacheHeadersMiddleware` | Documentar headers e limites na referência e no portal |
| 6 | Tag única `Fisga.Api`, sem `operationId`, `summary` ou `description` | `openapi.json` | Tags por domínio, `operationId` estável e descrições de negócio |
| 7 | Upload `multipart/form-data` sem schema | `/api/midia`, `/api/especies/sugerir` | Schema multipart com campo, tipos e tamanho |
| 8 | Admin responde `404` em vez de `401/403` | `NewsletterEndpoints.TokenValido` | Documentar como decisão intencional, com aviso |
| 9 | Endpoints de auth com efeito colateral em `GET` e `302` | `/api/auth/confirmar`, `/api/auth/google/*` | Documentar redirects, cookies emitidos e estados |
| 10 | `info` incompleto e sem `servers` | `openapi.json` | `contact`, `license`, descrição e `servers` por ambiente |
| 11 | `/api/catalogo` não aparece no resumo manual do README | `PescariaEndpoints.cs` × `README.md` | Alinhar o resumo de produto ao inventário |
| 12 | `429` sai sem corpo e sem `Retry-After` | Configuração do rate limiter | Decidir se adiciona `Retry-After` e corpo de erro padronizado |
| 13 | `GET /api/auth/me` é anônimo e devolve `200` | `AuthEndpoints.cs` | Documentar que a ausência de sessão é `200` com `autenticado:false` |

## Verificação em execução local

Execução em 2026-11-20 com SQLite temporário, `Email:Provider=Console` e
`Sentry:Dsn` vazio. Somente o comportamento público foi exercitado; rotas
autenticadas foram conferidas no código e nos testes.

| Verificação | Resultado esperado | Resultado em 2026-11-20 |
| --- | --- | --- |
| `GET /health` | `200` com `status` e `version` | ✅ `200` `{"status":"ok","version":"dev"}`, `private, no-store` |
| `GET /ready` | `200` com `status:"ready"` | ✅ `200` `{"status":"ready"}` |
| `GET /api/legal/versoes` | `200` com as duas versões | ✅ `200` `{"termosVersao":"2026-11-17","privacidadeVersao":"2026-11-17"}`, `public, max-age=86400` |
| `GET /api/catalogo` | `200` com o catálogo | ✅ `200` com `especies`, `iscas` e demais listas, `public, max-age=86400` |
| `GET /api/condicoes` sem `lat`/`lon` | `400` `local_sem_coordenadas` | ✅ `400` `{"codigo":"local_sem_coordenadas","detalhe":"Informe a latitude e a longitude do local."}` |
| `GET /api/condicoes?lat=0&lon=0` | `200` ou erro de provedor documentado | ✅ `200` com `fonte:"Open-Meteo + MET Norway"`, `mareAproximada:true` e `aviso` de modelo global |
| `GET /api/locais/buscar?q=` | `200` lista vazia | ✅ `200` `[]`, `private, max-age=3600` |
| `GET /api/auth/me` sem cookie | `200` com `autenticado:false` | ✅ `200` `{"autenticado":false,"usuario":null,"consentimentoPendente":false,"googleConectado":false,"temSenha":false}` |
| `GET /api/diario` sem cookie | `401` `nao_autenticado` | ✅ `401` `{"codigo":"nao_autenticado","detalhe":"Entre na sua conta para continuar."}` |
| `GET /api/newsletter/preview` sem token | `404` | ✅ `404` sem corpo |
| `POST /api/newsletter/enviar` sem token | `404` | ✅ `404` sem corpo |
| `GET /api/pescarias/inexistente` | `404` com `codigo` | ✅ `404` `{"codigo":"pescaria_nao_encontrada","detalhe":"Pescaria não encontrada."}` |
| `POST /api/auth/sair` com `Origin` externa | `403` `origem_invalida` | ✅ `403` `{"codigo":"origem_invalida","detalhe":"Origem da requisição não permitida."}` |
| `POST /api/auth/sair` sem `Origin` | `204` | ✅ `204 No Content` |
| 10 × `POST /api/auth/solicitar` | 8 aceitos, depois `429` | ✅ oito `202`, depois `429` sem corpo e **sem `Retry-After`** |
| `GET /openapi/v1.json` em Development | `200` | ✅ `200` (rota existe só fora de produção) |

> **Nota:** o horário do servidor e os provedores externos (maré, clima,
> geocodificação, visão) podem variar. A verificação valida contrato e
> autenticação, não a qualidade do dado externo.
