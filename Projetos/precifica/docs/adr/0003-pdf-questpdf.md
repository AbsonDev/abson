# ADR 0003 — PDF com QuestPDF e fonte embarcada

**Status:** aceito · **Data:** 2026-11-15

## Contexto

O PDF é parte do produto (apresentação para o cliente e marketing). Precisa ser gerado no servidor,
sem navegador headless, com layout estável em Linux/Coolify e QR Code.

## Decisão

- **QuestPDF** para o documento, com licença Community (receita anual < US$ 1M).
- **QRCoder** (`PngByteQRCode`) para o QR Code, sem dependência de `System.Drawing` no servidor.
- Fontes **Lato Regular/Bold** embarcadas como recursos (OFL), garantindo o mesmo desenho em
  qualquer container.
- PDF gratuito com marca d’água diagonal e CTA; PDF premium sem marca, com rodapé Pro.
- O caso de uso `ServicoDePdf` decide o acesso; o gerador não conhece pagamento.

## Consequências

- Geração síncrona e rápida, sem infraestrutura extra.
- Um único layout para web e PDF? Não: o PDF tem composição própria, mas usa os mesmos números do
  domínio.
- Se a receita ultrapassar o limite da licença, comprar a licença QuestPDF antes de continuar.
