# ADR 0012 — Ferramentas, EXIF e sugestão de espécie

**Status:** parcialmente substituído pelo [ADR 0016](0016-confirmacao-exif.md) · **Data:** 2026-11-17

## Contexto

O pescador precisa de nós, checklists e defeso antes de sair e de menos digitação ao voltar. O
diário já guarda condições, mas ainda exige que ele preencha data, cidade e espécie. Também existe a
promessa de reconhecer espécie por foto, que depende de um provedor externo e de confirmação humana.

## Decisão

- **Ferramentas são estáticas e offline**: nós com passo a passo, checklists por modalidade,
  defeso por UF e guia de escolha de piloteiro. As páginas entram no cache do PWA.
- **Defeso é editorial e aponta para a fonte oficial.** O Fisga não mantém uma tabela de tamanhos
  mínimos por espécie/UF nesta fase, porque a regra muda por bacia, ano e portaria. O produto
  mostra o órgão, o que confirmar e um lembrete no registro.
- **EXIF**: data/hora e GPS da foto são lidos no navegador apenas como sugestão. Data, período,
  cidade e UF só preenchem campos vazios depois da confirmação explícita da pessoa
  ([ADR 0016](0016-confirmacao-exif.md)); `local` nunca recebe valor do GPS. O GPS não é armazenado
  sem ação do usuário e a sugestão pode ser corrigida.
- **Sugestão de espécie é uma porta plugável** (`IReconhecedorDeEspecie`):
  - `Manual` é o padrão e devolve “confirme manualmente”, sem inventar;
  - `Gemini` é opcional via `Vision__Provider`, `Vision__ApiKey` e `Vision__Model`;
  - a resposta é limitada ao catálogo do Fisga, com confiança e no máximo 3 sugestões;
  - o pescador confirma ou corrige antes de salvar.
- **Insights comparativos** usam média de capturas por pescaria em cada condição, com pelo menos 3
  pescarias em cada lado e diferença mínima de 15%. A UI mostra “sinal, não garantia”.
- **Trilhas** são privadas, com no máximo 500 pontos e 100 trilhas por conta; a distância é
  calculada por Haversine e o usuário pode exportar GPX.
- **Azulejos do mapa** visitados entram em um cache separado, limitado a 600 tiles. Não é um mapa
  offline completo.

## Consequências

- O usuário ganha valor antes de configurar qualquer IA ou conta paga.
- A sugestão de espécie fica pronta para ligar sem reescrever o fluxo; sem chave, o produto continua
  honesto.
- O defeso não promete precisão legal que o Fisga não tem; o link oficial é a fonte de verdade.
- EXIF melhora a data e a cidade, mas não substitui a conferência das condições do momento.
- O cache de tiles pode ocupar espaço no aparelho; o limite e a limpeza ficam no service worker.
- O diretório de guias continua em curadoria; não há cadastro automático nem pagamento.
