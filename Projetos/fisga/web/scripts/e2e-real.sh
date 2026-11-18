#!/usr/bin/env bash
# E2E de identidade com backend real (Task 13).
#
# Sobe a API em Development com SQLite, chaves de Data Protection e outbox de
# e-mail descartáveis, espera /health e roda o Playwright real. A API e os dados
# temporários são removidos ao final. A web usa o dev server do Playwright
# (ou um já em execução na porta 4323).
set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
web="$raiz/web"
tmp="$(mktemp -d "${TMPDIR:-/tmp}/fisga-e2e.XXXXXX")"
api_pid=""

limpar() {
  if [[ -n "$api_pid" ]]; then
    kill "$api_pid" 2>/dev/null || true
    wait "$api_pid" 2>/dev/null || true
  fi
  rm -rf "$tmp"
}
trap limpar EXIT

if curl -fsS http://localhost:5184/health >/dev/null 2>&1; then
  echo "Já existe uma API em http://localhost:5184. Pare-a antes do E2E real." >&2
  exit 1
fi

if curl -fsS http://localhost:4324 >/dev/null 2>&1; then
  echo "Já existe um servidor web em http://localhost:4324. O E2E real exige a porta livre." >&2
  exit 1
fi

dotnet_bin="${DOTNET_BIN:-$HOME/.dotnet/dotnet}"
command -v "$dotnet_bin" >/dev/null 2>&1 || dotnet_bin="dotnet"

"$dotnet_bin" build "$raiz/src/Fisga.Api" -v quiet

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://localhost:5184 \
Database__Provider=Sqlite \
Database__ConnectionString="Data Source=$tmp/fisga-e2e.db" \
DataProtection__KeysPath="$tmp/keys" \
Email__Provider=Outbox \
Email__OutboxPath="$tmp/outbox" \
Auth__LinkBaseUrl=http://localhost:5184 \
Auth__WebUrl=http://localhost:4324 \
Auth__CookieSecure=false \
Cors__AllowedOrigins=http://localhost:4324 \
Web__Url=http://localhost:4324 \
Google__ClientId="" \
Google__ClientSecret="" \
RateLimiting__Auth=1000 \
Auth__IntervaloDeReenvioEmSegundos=0 \
SENTRY_DSN="" \
"$dotnet_bin" "$raiz/src/Fisga.Api/bin/Debug/net10.0/Fisga.Api.dll" \
  >"$tmp/api.log" 2>&1 &
api_pid=$!

for _ in $(seq 1 120); do
  if curl -fsS http://localhost:5184/health >/dev/null 2>&1; then
    break
  fi
  if ! kill -0 "$api_pid" 2>/dev/null; then
    echo "A API encerrou antes de ficar saudável:" >&2
    tail -n 80 "$tmp/api.log" >&2
    exit 1
  fi
  sleep 1
done

if ! curl -fsS http://localhost:5184/health >/dev/null 2>&1; then
  echo "A API não respondeu em /health:" >&2
  tail -n 80 "$tmp/api.log" >&2
  exit 1
fi

cd "$web"
E2E_REAL=1 \
E2E_OUTBOX="$tmp/outbox" \
E2E_API_URL=http://localhost:5184 \
E2E_WEB_URL=http://localhost:4324 \
  npx playwright test --config=playwright.real.config.ts "$@"
