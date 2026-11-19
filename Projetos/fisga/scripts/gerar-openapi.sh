#!/usr/bin/env bash
# Gera docs/api/openapi.json a partir do código.
#
# A geração sobe a aplicação em memória; por isso usa Development com SQLite
# temporário e Sentry desligado. Nunca use este script com dados reais.
set -euo pipefail

command -v dotnet >/dev/null || { echo "dotnet não encontrado no PATH" >&2; exit 1; }

raiz=$(cd "$(dirname "$0")/.." && pwd)
origem="$raiz/src/Fisga.Api/artifacts/openapi/Fisga.Api.json"
destino="$raiz/docs/api/openapi.json"
temporario=$(mktemp -d)
trap 'rm -rf "$temporario"' EXIT

export ASPNETCORE_ENVIRONMENT=Development
export Database__ConnectionString="Data Source=$temporario/fisga-openapi.db"
export DataProtection__KeysPath="$temporario/keys"
export Sentry__Dsn=""

dotnet build "$raiz/src/Fisga.Api/Fisga.Api.csproj" \
  -c Release \
  -p:RestoreLockedMode=true \
  -p:GenerateOpenApiDocs=true

mkdir -p "$(dirname "$destino")"
cp "$origem" "$destino"
printf '\n' >> "$destino"
printf 'OpenAPI gerado em %s\n' "$destino"
