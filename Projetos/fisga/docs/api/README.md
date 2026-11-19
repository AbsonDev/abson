# Contrato da API

`openapi.json` nesta pasta é o contrato publicado da API do Fisga. Ele é
**gerado** a partir do código (`Microsoft.AspNetCore.OpenApi`) e versionado no
repositório para que clientes, revisores e agentes leiam o mesmo documento.
Não edite o JSON à mão: a próxima geração sobrescreve.

## Regenerar

Na raiz de `Projetos/fisga`:

```bash
./scripts/gerar-openapi.sh
```

O script builda a API, extrai o documento e escreve `docs/api/openapi.json`.
Commite o arquivo gerado no mesmo PR da mudança de contrato.

## De onde vem o documento

- Rotas registradas em `src/Fisga.Api/Endpoints/` e os dois endpoints de saúde
  em `src/Fisga.Api/Program.cs`.
- DTOs de entrada e saída em `src/Fisga.Api/Contracts/`.
- A contagem de paths e schemas muda a cada mudança de contrato e não é
  mantida à mão aqui: consulte o próprio `openapi.json`, que é a fonte da
  verdade.

## Limites conhecidos

- **Só existe em Development.** O `MapOpenApi()` fica dentro de
  `if (app.Environment.IsDevelopment())`, então `/openapi/v1.json` responde
  apenas no ambiente local. Em produção o documento não é exposto; a fonte é o
  `openapi.json` versionado aqui.
- **Autenticação por cookie não aparece como `securityScheme`.** A sessão do
  Fisga é um cookie assinado tratado por middleware, não um esquema declarado
  no documento. Ferramentas que geram clientes a partir do contrato não
  mostram o requisito de login; nos endpoints protegidos, a resposta sem
  sessão é `401`.
- **Cache e rate limiting são metadados de runtime**, não fazem parte do
  contrato gerado.
