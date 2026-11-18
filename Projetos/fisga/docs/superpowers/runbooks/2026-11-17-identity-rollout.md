# Runbook — rollout da identidade (Identity 2A)

**Data:** 18/11/2026 · **Escopo:** API `api.fisga.abson.tech` e web `fisga.abson.tech`
**Plano:** [`docs/superpowers/plans/2026-11-17-identity-google.md`](../plans/2026-11-17-identity-google.md)
**Migração:** `20261117213339_IdentityConsentimento`

Este runbook cobre o deploy da autenticação baseada em ASP.NET Core Identity: senha, link mágico,
Google opcional, consentimento formal, sessões por dispositivo e Data Protection persistente.

> **Regra de ouro:** a migration é **aditiva**. Nenhum passo deste runbook apaga tabela, coluna ou
> dado. Rollback é por **imagem anterior + backup**, nunca por operação destrutiva de schema.

---

## 1. O que muda no deploy

| Área | Antes | Depois |
| --- | --- | --- |
| Cookie de sessão | `fisga_sessao` (próprio) | `fisga_auth` (Identity), `HttpOnly`, `Secure`, `SameSite=Lax` |
| Login | Somente link mágico | Senha + link mágico + Google opcional |
| Consentimento | Implícito | `/aceite` obrigatório para Termos/Privacidade `2026-11-17` |
| Sessões | Uma linha por login | Linha por dispositivo com “sair desta” e “sair de todos” |
| Schema | — | Tabelas `AspNet*`, `consentimentos` e índices (aditivo) |
| Chaves | — | Data Protection em `/app/data/keys` (volume persistente) |

`fisga_sessao` deixa de autenticar. Ele é apagado no login/logout pela chave
`Auth__LegacyCookieName` (padrão `fisga_sessao`). A chave antiga `Auth__CookieName` foi
**renomeada**; se ela ainda existir no ambiente, remova-a para não confundir a operação.

### Contas existentes

Não há backfill de `AspNetUsers`. O usuário antigo continua no banco em `fisga.usuarios` e ganha a
conta Identity **no primeiro login** por link mágico. Nenhuma pescaria, local, ponto ou trilha é
tocada. O primeiro login após o deploy pede o aceite formal uma única vez.

---

## 2. Pré-requisitos e decisões

- [ ] **Data Protection persistente é obrigatório.** Sem o volume em `/app/data/keys`, cada deploy
  invalida o cookie `fisga_auth`, o cookie externo do Google e os links de confirmação/reset em
  circulação.
- [ ] **Banco de produção:** PostgreSQL 17. A migration sobe no start da API
  (`DatabaseInitializer` chama `MigrateAsync`). Não rode `dotnet ef database update` contra produção
  sem backup e janela.
- [ ] **E-mail:** `Email__Provider=Resend` com chave válida e domínio verificado. Sem isso,
  confirmação e reset não chegam ao usuário.
- [ ] **Google é opcional.** Sem `Google__ClientId`/`Google__ClientSecret`, a API sobe normalmente,
  `/api/auth/google/disponivel` responde `false` e o botão não aparece.
- [ ] **Janela sugerida:** 30–60 minutos, fora do pico. O deploy da API é o único momento que exige
  atenção; web e API podem ficar alguns segundos em versões diferentes porque os contratos novos são
  retrocompatíveis.
- [ ] **Comunicação:** avisar 24 h antes que usuários podem precisar entrar de novo (texto na seção 9).

---

## 3. Checklist pré-deploy

### 3.1 Backup

```bash
# Execute com a connection string de produção vinda do cofre de segredos.
pg_dump --format=custom --no-owner --file="fisga-pre-identity-$(date +%Y%m%d-%H%M).dump" "$DATABASE_URL"
```

- [ ] Backup concluído e testado em um banco descartável (`pg_restore --list`).
- [ ] Snapshot/cópia segura do volume de chaves, se ele já existir. As chaves são segredo: guarde no
  mesmo cofre do backup do banco, com acesso restrito.
- [ ] Anotar o **digest da imagem atual** da API e da web para rollback.

### 3.2 Variáveis de ambiente da API

Nunca commite valores. Configure no cofre/Coolify:

| Variável | Valor esperado em produção |
| --- | --- |
| `Database__Provider` | `Postgres` |
| `Database__ConnectionString` | conexão do PostgreSQL de produção |
| `Auth__LinkBaseUrl` | `https://api.fisga.abson.tech` |
| `Auth__WebUrl` | `https://fisga.abson.tech` |
| `Auth__CookieSecure` | `true` |
| `Auth__LegacyCookieName` | `fisga_sessao` (só para apagar o cookie obsoleto) |
| `DataProtection__KeysPath` | `/app/data/keys` |
| `Cors__AllowedOrigins` | `https://fisga.abson.tech,https://www.fisga.abson.tech` |
| `Web__Url` | `https://fisga.abson.tech` |
| `Email__Provider` | `Resend` |
| `Email__From` | `Fisga <acesso@abson.tech>` |
| `Email__ResendApiKey` | chave do Resend (segredo) |
| `Legal__TermsVersion` | `2026-11-17` |
| `Legal__PrivacyVersion` | `2026-11-17` |
| `SENTRY_DSN` | DSN do projeto `fisga-api` |

> `Auth__CookieName` não é mais lida. Remova-a do ambiente para evitar configuração morta.

### 3.3 Volume de chaves

No `docker-compose.yml` deste repositório o serviço `api` monta:

```yaml
volumes:
  - fisga_media:/app/media
  - fisga_keys:/app/data/keys
```

Em produção (Coolify/host), o mount precisa existir com o **mesmo caminho**:

- tipo: volume nomeado ou bind mount persistente;
- destino: `/app/data/keys`;
- dono/escrita: usuário do container da API;
- **não** montar `tmpfs`, **não** usar diretório efêmero do container;
- incluir o volume no backup seguro.

Sem esse mount, o primeiro redeploy/recreate do container (ou qualquer restart que recrie a camada
gravável) já invalida todos os cookies.

### 3.4 DNS, TLS e proxy

- [ ] `api.fisga.abson.tech` e `fisga.abson.tech` com TLS válido.
- [ ] Proxy repassa `X-Forwarded-Proto`/`X-Forwarded-For` (a API já usa `UseForwardedHeaders`).
- [ ] `https://api.fisga.abson.tech/signin-google`, `/api/auth/google/concluir` e
  `/api/auth/google/conectar/concluir` não são bloqueados por WAF/rewrite.

---

## 4. Ensaio da migration em PostgreSQL local

> **Status neste ambiente: NÃO EXECUTADO.** O `docker` não está instalado nesta máquina e não há
> PostgreSQL local disponível. O procedimento abaixo é o checklist obrigatório antes do deploy e
> deve ser executado por quem tiver Docker/Postgres. Não trate este ensaio como concluído.

O ensaio usa um banco **descartável**, começa vazio, aplica a migration anterior, semeia um usuário
legado sem conta Identity e uma pescaria, e só então aplica a migration de identidade. Nunca use
produção neste passo.

```bash
cd Projetos/fisga

# Banco descartável: apaga apenas o volume local de ensaio.
docker compose down -v
docker compose up -d postgres

export PGPASSWORD=fisga

~/.dotnet/dotnet tool restore

# 0) Garante que o modelo bate com a última migration (sem drift).
Database__Provider=Postgres \
Database__ConnectionString='Host=localhost;Port=5435;Database=fisga;Username=fisga;Password=fisga' \
~/.dotnet/dotnet ef migrations has-pending-model-changes \
  --project src/Fisga.Infrastructure \
  --startup-project src/Fisga.Api
# esperado: "No changes have been made to the model since the last migration."

# 1) Aplica até a migration ANTERIOR (Sprint4), deixando o banco sem AspNetUsers.
Database__Provider=Postgres \
Database__ConnectionString='Host=localhost;Port=5435;Database=fisga;Username=fisga;Password=fisga' \
~/.dotnet/dotnet ef database update 20261117164902_Sprint4 \
  --project src/Fisga.Infrastructure \
  --startup-project src/Fisga.Api

# 2) Confere que a tabela nova ainda não existe.
psql -h localhost -p 5435 -U fisga -d fisga -tAc \
  "select coalesce(to_regclass('fisga.\"AspNetUsers\"')::text, 'ausente');"
# esperado: ausente

# 3) Semeia um usuário legado (sem AspNetUsers) e uma pescaria dele.
psql -h localhost -p 5435 -U fisga -d fisga -v ON_ERROR_STOP=1 <<'SQL'
insert into fisga.usuarios (
  "Id", "AceitaNovidades", "AtualizadoEm", "BemVindoEm", "CriadoEm",
  email, "EmailVerificadoEm", "Nome", "UltimoAcessoEm"
) values (
  '00000000-0000-0000-0000-000000000001', false, now(), null, now(),
  'legado@exemplo.com', now(), 'Usuário Legado', null
);

insert into fisga.pescarias (
  "Id", "Ambiente", "AtualizadoEm", "Cidade", "ClarezaDaAgua", "Clima",
  "CriadoEm", "Data", "Estado", "Local", "Lua", "Mare", "Observacoes",
  "Periodo", "Pescador", "PrivacidadeDoLocal", "ShareToken", "UsuarioId"
) values (
  '00000000-0000-0000-0000-000000000002', 'Mar', now(), 'Aracaju', 'Clara', 'Sol',
  now(), '2026-11-10', 'SE', 'Praia Legada', 'Crescente', 'Vazante',
  'Seed do ensaio de migration', 'Manha', 'Pescador Legado', 'Cidade',
  'share-legado-0001', '00000000-0000-0000-0000-000000000001'
);
SQL

# 4) Contagens ANTES: usuarios = 1, pescarias = 1, AspNetUsers ausente.
psql -h localhost -p 5435 -U fisga -d fisga -tAc 'select count(*) from fisga.usuarios;'
psql -h localhost -p 5435 -U fisga -d fisga -tAc 'select count(*) from fisga.pescarias;'
psql -h localhost -p 5435 -U fisga -d fisga -tAc \
  "select coalesce(to_regclass('fisga.\"AspNetUsers\"')::text, 'ausente');"

# 5) Aplica a migration de identidade.
Database__Provider=Postgres \
Database__ConnectionString='Host=localhost;Port=5435;Database=fisga;Username=fisga;Password=fisga' \
~/.dotnet/dotnet ef database update \
  --project src/Fisga.Infrastructure \
  --startup-project src/Fisga.Api

# 6) Contagens DEPOIS: usuarios = 1, pescarias = 1, AspNetUsers = 0 (sem backfill).
psql -h localhost -p 5435 -U fisga -d fisga -tAc 'select count(*) from fisga.usuarios;'
psql -h localhost -p 5435 -U fisga -d fisga -tAc 'select count(*) from fisga.pescarias;'
psql -h localhost -p 5435 -U fisga -d fisga -tAc 'select count(*) from fisga."AspNetUsers";'
psql -h localhost -p 5435 -U fisga -d fisga -tAc \
  'select "MigrationId" from fisga."__EFMigrationsHistory" order by 1;'
```

**Resultado esperado**

| Tabela | Antes | Depois |
| --- | --- | --- |
| `fisga.usuarios` | 1 | **1** (inalterado) |
| `fisga.pescarias` | 1 | **1** (inalterado) |
| `fisga."AspNetUsers"` | ausente | **0** (tabela criada, sem backfill; cresce no primeiro login) |
| `__EFMigrationsHistory` | até `Sprint4` | contém `20261117213339_IdentityConsentimento` |

Faça também um smoke no ensaio: suba a API apontando para esse banco, entre com
`legado@exemplo.com` por link mágico, aceite os termos e confira que “Praia Legada” aparece no
diário. Só então agende produção.

Para limpar o ensaio local (destrutivo, somente local):

```bash
docker compose down -v
```

---

## 5. Ordem do deploy

1. **Congelar mudanças de schema** e iniciar a janela.
2. **Backup** do banco e das chaves (seção 3.1).
3. **Configurar envs e volume** (seções 3.2 e 3.3), sem trocar a imagem ainda.
4. **Deploy da API** (nova imagem). O start aplica a migration aditiva.

   > **Janela API → web:** a API nova ignora o cookie antigo `fisga_sessao`; até a web nova
   > terminar o deploy, quem ainda tem só esse cookie fica efetivamente deslogado. Faça os dois
   > deploys em sequência, sem pausa longa, e avise que um novo login pode ser pedido.

5. **Verificar API:**
   ```bash
   curl -fsS https://api.fisga.abson.tech/health
   curl -fsS https://api.fisga.abson.tech/ready
   ```
   `ready` confirma conexão com o banco. Se falhar, pare e investigue antes de seguir.
6. **Deploy da web** (nova imagem).
7. **Verificar web:**
   ```bash
   curl -fsS https://fisga.abson.tech/health
   ```
8. **Smoke de login** (seção 6).
9. **Monitorar** Sentry, logs do proxy e taxa de 401/403/500 por 30–60 minutos.

A API aceita a web antiga por alguns segundos (contratos antigos continuam válidos), mas não deixe
as duas versões conviverem por muito tempo.

---

## 6. Verificação pós-deploy (`/health` e login de teste)

### 6.1 Saúde

- [ ] `GET https://api.fisga.abson.tech/health` → `{"status":"ok"}`
- [ ] `GET https://api.fisga.abson.tech/ready` → `{"status":"ready"}`
- [ ] `GET https://fisga.abson.tech/health` → `{"status":"ok"}`
- [ ] Containers `fisga-api` e `fisga-web` sem restart loop.
- [ ] Sentry sem novos erros de migration/Data Protection.

### 6.2 Login de teste

Use uma conta de teste real, nunca uma conta de cliente:

1. **Usuário antigo (link mágico):** `/entrar` → “Enviar link de acesso” → abrir o e-mail →
   `/aceite` → aceitar → app. Conferir que o diário e as pescarias antigas continuam.
2. **Novo usuário (senha):** `/criar-conta` → e-mail de confirmação → `/confirmar-email` → entrar →
   `/aceite` → app.
3. **Reset:** `/esqueci-senha` → e-mail → `/redefinir-senha` → nova senha → entrar. Conferir que a
   senha antiga falha.
4. **Sessões:** `/conta` → “Sair desta sessão” e, em outro navegador, “Sair de todos”. O outro
   dispositivo deve perder o acesso.
5. **Conta:** criar senha (conta de link mágico), alterar senha, salvar nome/newsletter, exportar
   JSON e conferir que o cookie continua válido após a troca.
6. **Cookies:** DevTools → `fisga_auth` com `HttpOnly`, `Secure`, `SameSite=Lax`; `fisga_sessao`
   ausente após login/logout.
7. **Google (só se configurado):** botão “Continuar com Google” visível; login e conexão em `/conta`
   funcionam; e-mail divergente é recusado.
8. **Logs:** não deve haver e-mail completo, token, `uid` ou URL de confirmação/reset em log. O
   `ConsoleEmailSender` registra apenas domínio/assunto; em produção o Resend não loga corpo.

Se qualquer item falhar: pare a janela, avalie rollback (seção 8) e não “conserte em produção”.

---

## 7. Google OAuth — configuração e checklist pós-credenciais

> **Status neste ambiente: NÃO EXECUTADO.** Não há credenciais Google reais. O E2E cobre apenas o
> estado “indisponível” (botão oculto e endpoint `false`).

No Google Cloud Console, crie/ajuste um OAuth Client do tipo **Web application**:

- **Authorized JavaScript origins**
  - `https://fisga.abson.tech`
  - `https://www.fisga.abson.tech` (se usado)
- **Authorized redirect URIs**
  - `https://api.fisga.abson.tech/signin-google` ← callback OAuth exigido pelo handler
- Redirects internos após o callback (não vão no Console, mas precisam passar pelo proxy):
  - `https://api.fisga.abson.tech/api/auth/google/concluir` (login)
  - `https://api.fisga.abson.tech/api/auth/google/conectar/concluir` (conectar em `/conta`)

Configure na API:

```env
Google__ClientId=...
Google__ClientSecret=...
Google__CallbackPath=/signin-google
```

Checklist após as credenciais:

- [ ] API reimplantada/reiniciada e `GET /api/auth/google/disponivel` → `{"disponivel":true}`.
- [ ] `/entrar` mostra “Continuar com Google” e o divisor “ou”.
- [ ] Login com e-mail Google já confirmado no Fisga entra sem pedir senha.
- [ ] Google com e-mail não confirmado localmente é recusado com mensagem clara.
- [ ] `/conta` → “Conectar Google” vincula a conta atual sem criar outra sessão/usuário.
- [ ] Mesmo `sub` não pode ser vinculado a duas contas.
- [ ] Conexão com e-mail divergente é recusada.
- [ ] Após reconectar, não há linha duplicada em `AspNetUserLogins`.
- [ ] Cookies de estado externo somem após sucesso/falha.

Se o ClientSecret for rotacionado, atualize a env, reimplante/reinicie a API e refaça o login de
teste. Não remova o volume de chaves ao trocar credenciais.

---

## 8. Rollback

**Forma oficial:** reimplantar a imagem anterior (digest anotado) e, se houve problema de dados,
restaurar o backup.

### 8.0 Pré-rollback obrigatório: revogar sessões

Antes de reimplantar a imagem antiga, encerre as sessões no banco. A imagem antiga autentica pelo
hash em `fisga.sessoes`; sem revogar, um cookie `fisga_sessao` antigo ainda válido poderia autenticar
depois do rollback.

```bash
# Defina com os valores reais do cofre; não commite.
export PGHOST='...'
export PGPORT='5432'
export PGUSER='...'
export PGPASSWORD='...'
export PROD_DB='fisga'

psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PROD_DB" -v ON_ERROR_STOP=1 \
  -c 'UPDATE fisga.sessoes SET "RevogadaEm" = NOW() WHERE "RevogadaEm" IS NULL;'
```

- [ ] Confirmar que `select count(*) from fisga.sessoes where "RevogadaEm" is null;` retorna 0.
- [ ] Comunicar que o rollback exige um novo login (seção 9).

### 8.1 Rollback só de código (preferido)

1. Execute 8.0 (revogar sessões).
2. Reimplante o digest anterior da API e da web.
3. **Mantenha** o banco migrado e o volume `fisga_keys`. A migration é aditiva; a imagem antiga
   ignora as tabelas novas.
4. Verifique `/health` e `/ready`.
5. Teste login por link mágico na versão antiga.

### 8.2 Rollback com restauração de dados

Use somente se houver corrupção/perda de dados. A restauração é uma **operação manual separada**;
a regra “nenhuma operação destrutiva” vale para o caminho de migration/deploy, não para este
procedimento de incidente. **Nunca** use `pg_restore --clean` apontando para produção.

1. Pare a API e a web.
2. Execute 8.0 (revogar sessões) no banco atual.
3. Restaure o dump em um banco **novo e vazio**; valide contagens e só então troque a connection
   string.

```bash
# Defina explicitamente; nunca aponte RESTORE_DB para o banco de produção.
export PROD_DB="${PROD_DB:?Defina PROD_DB com o nome real do banco de produção}"
RESTORE_DB="fisga_restore_$(date +%Y%m%d_%H%M%S)"
DUMP='fisga-pre-identity-AAAAMMDD-HHMM.dump'

if [ "$RESTORE_DB" = "$PROD_DB" ]; then
  echo 'Destino de restauração não pode ser o banco de produção.' >&2
  exit 1
fi

if psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres -tAc \
  "select 1 from pg_database where datname = '$RESTORE_DB';" | grep -q 1; then
  echo "Banco $RESTORE_DB já existe; escolha outro." >&2
  exit 1
fi

createdb -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$RESTORE_DB"

# Deve ser 0: banco recém-criado, sem tabelas de usuário.
test "$(psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$RESTORE_DB" -tAc \
  "select count(*) from pg_tables where schemaname not in ('pg_catalog','information_schema');")" = '0'

pg_restore --no-owner --no-privileges \
  --dbname="host=$PGHOST port=$PGPORT user=$PGUSER dbname=$RESTORE_DB" \
  "$DUMP"

psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$RESTORE_DB" -c \
  'select (select count(*) from fisga.usuarios) as usuarios, (select count(*) from fisga.pescarias) as pescarias;'
```

4. Compare as contagens com o backup/incidente e valide o login em uma instância de ensaio.
5. Aponte a connection string de produção para o banco restaurado.
6. Suba a imagem anterior.
7. Verifique `/health`, `/ready` e o login de teste.

**Nunca** rode `dotnet ef database update` para “voltar” a migration em produção. Não há operação
destrutiva prevista no caminho normal de deploy.

### 8.3 Efeitos do rollback

- Cookies `fisga_auth` não são lidos pela imagem antiga: usuários precisam entrar de novo por link
  mágico.
- As sessões foram revogadas no passo 8.0; cookies `fisga_sessao` antigos também não autenticam.
- Usuários criados **somente com senha** após o deploy não têm senha utilizável na imagem antiga;
  devem usar link mágico.
- Consentimentos e sessões novas permanecem no banco; a imagem antiga os ignora.
- O volume de chaves deve continuar montado: se voltar para a versão nova depois, as chaves antigas
  preservam os cookies emitidos antes do rollback.

---

## 9. Comunicação de re-login

Enviar 24 h antes e reforçar no dia:

> **Assunto:** Atualizamos o acesso ao Fisga
>
> Atualizamos a forma de entrar no Fisga para deixar sua conta mais segura. Por isso, talvez seja
> preciso entrar de novo no próximo acesso. Use “Entrar com link de acesso” ou sua senha — seu
> diário, suas pescarias e seus locais continuam intactos.
>
> Se você não lembrar a senha, use “Esqueci a senha” em `/entrar`.

Canais: e-mail para contas ativas, aviso no site e, se houver, redes sociais. Não incluir links com
token na comunicação. Registrar a data do envio no histórico da janela.

**Em rollback**, todas as sessões são revogadas antes da troca de imagem (seção 8.0). Nesse caso a
comunicação deve ser afirmativa: “encerramos as sessões ativas por segurança; entre novamente com
link de acesso ou senha”. Não prometa que a sessão atual será preservada.

---

## 10. Limpeza pós-rollout

- [ ] Remover a env morta `Auth__CookieName`.
- [ ] Remover contas de teste criadas no smoke (`*@exemplo.com` ou marcadas `e2e-*`) via `/conta` ou
  `DELETE /api/conta` com a própria sessão.
- [ ] Revogar credenciais temporárias usadas no ensaio (se houver) e limpar arquivos locais de
  outbox/backup temporário.
- [ ] Confirmar que o volume `fisga_keys` está no backup recorrente e com acesso restrito.
- [ ] Arquivar este runbook com data, digest das imagens, contagens antes/depois e link do backup.
- [ ] Revisar Sentry: sem PII, sem erros novos de `DataProtection`/`Identity`.
- [ ] Rodar `grep -E 'token=|uid='` nos logs da janela; o resultado deve ser vazio.

---

## 11. Evidências de teste (18/11/2026)

| Verificação | Resultado |
| --- | --- |
| `~/.dotnet/dotnet test` | **213 aprovados**, 0 falhas (36 domínio + 48 aplicação + 129 API) |
| `npm run check` | 0 erros, 0 avisos, 0 hints (89 arquivos) |
| `npm run build` | aprovado (Astro + Pagefind + precache) |
| Playwright mockado | **58/58 aprovados** |
| Playwright backend real (`scripts/e2e-real.sh`) | **6/6 aprovados** em 18,8 s; **12/12** com `--repeat-each=2` (37,4 s) |
| Migration em PostgreSQL local | **NÃO EXECUTADO** (sem Docker/Postgres nesta máquina) |
| OAuth Google real | **NÃO EXECUTADO** (sem credenciais) |
| Sentry em produção | **NÃO VERIFICADO** (DSN ausente no E2E) |

O E2E real cobre: senha → confirmação → consentimento → app; retorno por link mágico →
consentimento → diário com pescaria preservada; reset de senha; “sair desta” e “sair de todos” com
dois navegadores; `/conta` com criação/alteração de senha, perfil/newsletter, exportação JSON; Google
indisponível com botão oculto e endpoint `false`.

Reprodução local:

```bash
cd Projetos/fisga/web
./scripts/e2e-real.sh
```

O script cria SQLite, chaves e outbox em diretório temporário, sobe a API em `5184` e uma web
dedicada em `4324` (sem reutilizar dev server), espera `/health`, roda apenas
`identity-e2e.spec.ts` e limpa tudo ao final. Chromium do Playwright já estava instalado nesta
máquina.

---

## 12. Referências

- Plano: [`2026-11-17-identity-google.md`](../plans/2026-11-17-identity-google.md)
- Design: [`2026-11-17-identity-google-design.md`](../specs/2026-11-17-identity-google-design.md)
- Evidência de UX: [`docs/ux-audit.md`](../../ux-audit.md)
- Spec de UI: projeto OpenDesign `fisga-auth-screens`
