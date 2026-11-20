# Decisões arquiteturais (ADRs)

Uma ADR por decisão relevante. Nova decisão entra como `0018-<slug>.md` e
ganha linha nesta tabela.

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](0001-captura-com-identidade.md) | Captura com identidade própria | aceito |
| [0002](0002-diario-read-model.md) | Diário como read model privado por token | aceito |
| [0003](0003-foto-e-trofeu.md) | Foto no servidor, card do troféu no navegador | aceito |
| [0004](0004-login-link-magico.md) | Login por link mágico e sessão em cookie | substituído pelo [ADR 0013](0013-identity-completa.md) |
| [0005](0005-conta-obrigatoria.md) | Conta obrigatória para o diário | aceito |
| [0006](0006-email-resend.md) | E-mail transacional com Resend e console em desenvolvimento | aceito |
| [0007](0007-wiki-content-collections.md) | Wiki estática com Content Collections e Pagefind | aceito |
| [0008](0008-privacidade-local-e-fotos.md) | Privacidade do local e múltiplas fotos por captura | parcialmente substituído pelo [ADR 0015](0015-niveis-de-detalhe-do-local.md) (só os três valores; fotos permanecem) |
| [0009](0009-newsletter-mensal.md) | Newsletter mensal com descadastro assinado | aceito |
| [0010](0010-condicoes-e-locais.md) | Condições, locais salvos e janela de pesca | aceito |
| [0011](0011-mapa-privado.md) | Mapa privado e pontos do pescador | aceito |
| [0012](0012-ferramentas-exif-e-visao.md) | Ferramentas, EXIF e sugestão de espécie | parcialmente substituído pelo [ADR 0016](0016-confirmacao-exif.md) (só o preenchimento automático de EXIF) |
| [0013](0013-identity-completa.md) | Identidade completa com ASP.NET Core Identity | aceito |
| [0014](0014-registro-anonimo.md) | Registro anônimo: capturar primeiro, conta depois | aceito |
| [0015](0015-niveis-de-detalhe-do-local.md) | Cinco níveis de detalhe do local compartilhado | aceito |
| [0016](0016-confirmacao-exif.md) | Confirmação explícita dos dados da foto no registro | aceito |
| [0017](0017-portal-publico-da-api.md) | Portal público da API gerado a partir do contrato | aceito |
