# Fisga

**Diário de pesca com troféus, estatísticas e relatório em PDF.**

O pescador registra a pescaria, as condições do dia e cada captura — espécie, peso, comprimento,
isca, foto e se o peixe foi solto. O Fisga monta um diário privado, mostra padrões e gera um card de
troféu compartilhável + PDF da pescaria.

> **Estado atual: 100% gratuito.** Diário, card do troféu e PDF liberados durante a validação, sem
> cartão ou assinatura. Monetização futura prevista: R$ 9–19/mês.

## Funcionalidades do MVP

- Pescaria com local, cidade/UF, ambiente, data, período e condições (lua, maré, clima, água).
- Várias capturas por pescaria, com ID estável.
- Espécie, peso, comprimento, isca, destino (solto/capturado), observação e **foto**.
- Upload de foto com compressão no navegador (até 5 MB; JPG/PNG/WebP).
- Diário privado por token, com estatísticas:
  - pescarias, capturas, espécies distintas e peixes soltos;
  - maior peso e comprimento;
  - espécie, isca e local que mais aparecem.
- Link público por pescaria, sem expor o restante do diário.
- **Card do troféu** em PNG, gerado no navegador com foto, espécie, peso, local e data.
- **PDF da pescaria** com condições, capturas, maior peixe, notas e QR.
- Edição de pescaria preservando os IDs das capturas (links de troféu não quebram).
- Modo gratuito, sem checkout.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Domínio/aplicação | .NET 10 / C# 13 |
| API | ASP.NET Core 10, Minimal APIs, OpenAPI |
| Persistência | EF Core 10 + PostgreSQL 17 (SQLite no desenvolvimento) |
| PDF | QuestPDF + QRCoder |
| Mídia | upload local + arquivos estáticos com CORS |
| Card do troféu | Canvas API no navegador |
| Web | Astro 7 SSR + TypeScript + CSS próprio |
| Infra local | Docker Compose |

## DDD em uma frase

`Pescaria` é o agregado central (local, condições e `Captura`s com identidade); o **Diário** é um
read model privado por token que agrega pescarias e calcula estatísticas. Catálogo de espécies/iscas,
mídia e documentos são contextos de apoio. Não existe login no MVP.

Detalhes em [`docs/ddd.md`](docs/ddd.md) e [`docs/adr`](docs/adr).

## Rodando localmente

Pré-requisitos: **.NET 10 SDK** e **Node 24+**.

### API (SQLite)

```bash
cd Projetos/fisga/src/Fisga.Api
dotnet run
```

API em `http://localhost:5184`; o SQLite é criado no primeiro start. As fotos ficam em
`bin/Debug/net10.0/media`.

### Web

Em outro terminal:

```bash
cd Projetos/fisga/web
npm install
npm run dev
```

Abra **`http://localhost:4323`**.

Fluxo de teste:

1. Abra `/registrar`; o exemplo já vem preenchido.
2. Troque o local, adicione uma foto em uma captura e salve.
3. Veja a pescaria, abra o card do troféu e baixe a imagem/PDF.
4. Abra `/meu-diario` e confira as estatísticas.
5. Copie o link de backup do diário e abra em outra aba/aparelho.
6. Edite a pescaria: as capturas mantêm o mesmo ID.

### Docker Compose

```bash
cd Projetos/fisga
docker compose up --build
```

- Web: `http://localhost:3002`
- API: `http://localhost:5184`
- Postgres: `localhost:5435` (`fisga` / `fisga` / `fisga`)
- Fotos: volume `fisga_media` em `/app/media`

## Testes

```bash
cd Projetos/fisga
dotnet test
cd web && npm run check
```

Atualmente: **15 testes de backend** + checagem de tipos do Astro. Cobrem criação/edição de
pescaria, IDs de captura, validações, token do diário e estatísticas.

## API

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/catalogo` | espécies, iscas, opções e exemplo |
| `POST` | `/api/midia` | upload de foto (`multipart`, campo `arquivo`) |
| `POST` | `/api/pescarias` | cria pescaria; exige `X-Diary-Token` |
| `GET` | `/api/pescarias/{shareToken}` | visão pública |
| `PUT` | `/api/pescarias/{id}` | edita com `X-Edit-Token` |
| `GET` | `/api/pescarias/{shareToken}/pdf` | PDF da pescaria |
| `GET` | `/api/diario` | diário + estatísticas; exige `X-Diary-Token` |
| `GET` | `/health`, `/ready` | saúde/readiness |

O token do diário é gerado no navegador, tem 24 bytes aleatórios e o banco guarda apenas o SHA-256.
Cada pescaria tem `shareToken` público e `editToken` privado (hash no banco).

## Privacidade e limites

- O diário completo fica protegido por token no navegador; cada pescaria tem um link público próprio.
- O link de backup do diário dá acesso ao diário — trate como senha.
- Fotos são reencodadas no navegador antes do upload, o que remove metadados EXIF do arquivo.
- O local da pescaria aparece no link público daquela pescaria; o restante do diário não.
- O card do troféu é gerado localmente no navegador; o Fisga não publica em rede social.
- “Meu diário” no MVP não tem login. Workspace multiusuário, catálogo de locais, previsão de maré e
  identificação de espécies ficam para as próximas fases.

## Próximos passos

- Previsão de maré/lua/clima com APIs e sugestão de janela.
- Catálogo de locais e mapa privado.
- Identificação de espécie por foto e validação de tamanho/defeso.
- PWA offline para sinal ruim.
- Feed/comunidade e desafios entre pescadores.
- Assinatura R$ 9–19/mês após validação.

## Licença

Uso privado do portfólio. Defina a licença comercial antes de publicar como produto independente.
