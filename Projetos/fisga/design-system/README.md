# Design system do Fisga

Pacote versionado do design system do Fisga, no formato do OpenDesign:

```
fisga/
├── manifest.json   # descoberta: id, nome, categoria, arquivos
├── DESIGN.md       # prosa canônica para o agente
├── tokens.css      # tokens semânticos compilados
└── USAGE.md        # ordem de leitura e regras de uso
```

## Fonte da verdade

- **Código:** `Projetos/fisga/web/src/styles/global.css` (aliases de marca como
  `--papel`, `--terracota`).
- **UX:** `Projetos/fisga/docs/DESIGN-UX.md` (rotas, estados, componentes).
- **Este pacote:** traduz os aliases para os nomes semânticos exigidos pelo
  contrato do OpenDesign (`--bg`, `--fg`, `--accent`, `--text-*`, etc.).

Se um valor mudar no código, atualize `tokens.css` e `DESIGN.md` no mesmo PR.

## Como importar no OpenDesign (estado atual)

O pacote foi importado pelo fluxo oficial da interface:
**Design Systems → Create → Paste DESIGN.md**. O OpenDesign criou o projeto de
marca `brand-fisga-f7f0a3` e o design system `user:fisga-2` (status publicado).
O projeto canônico do produto é **Fisga — Produto** (`fisga-produto`), que já
está com `user:fisga-2` anexado e com o artefato da boas-vindas em
`boas-vindas/index.html`.

Não copie a pasta manualmente para
`~/Library/Application Support/Open Design/namespaces/.../data/design-systems/`:
o catálogo até lista o pacote, mas ele fica `draft` e o resolvedor de projeto
recusa o anexo (`DESIGN_SYSTEM_NOT_FOUND`). O fluxo da UI registra o design
system no workspace e evita esse bug.

Se precisar reimportar em outra máquina: use o mesmo caminho da UI e cole o
`DESIGN.md`; depois ajuste `brand.json`/`tokens.css` do projeto de marca se a
extração automática errar um valor.

## Como atualizar

1. Edite o pacote aqui (fonte versionada).
2. Atualize o projeto de marca `brand-fisga-f7f0a3` pelo fluxo da UI ou
   substituindo `DESIGN.md`, `tokens.css` e `brand.json` no diretório do
   projeto de design system.
3. O projeto `fisga-produto` herda o design system ativo; nenhum anexo novo é
   necessário.

Não edite o pacote direto no diretório do app sem atualizar o repo: o repo é a
fonte versionada.
