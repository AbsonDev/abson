# ADR 0010 — Condições, locais salvos e janela de pesca

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O Fisga era um diário: o pescador só abria o app depois de pescar e digitava tudo. O próximo salto
de valor é responder “vale a pena sair hoje?” antes da pescaria e reduzir a digitação no registro.
Também faltava um lugar para guardar os locais que o pescador usa de verdade.

A pesquisa de referências mostrou que maré confiável exige análise harmônica local (CHM/Marinha) e
que a maior parte dos sites de maré vive de anúncios e dados aproximados. Para esta fase, a regra é
usar apenas fontes gratuitas e nunca apresentar aproximação como dado oficial.

## Decisão

- **Local de pesca é agregado próprio**, privado, com nome, cidade/UF, coordenadas e ambiente.
- Limite de **5 locais por conta**, com um único padrão. Excluir o padrão promove o mais recente.
- **Condições** são agregadas de:
  - **Open-Meteo** para tempo, vento, chuva, pressão, ondas e nível do mar (modelo Copernicus);
  - **MET Norway** para nascer, pôr, trânsito e anti-trânsito da lua e ângulo de fase;
  - cálculo local para solunar, janela e nota.
- A **maré é aproximada** e sempre rotulada como tal. Não é tábua oficial e não serve para
  navegação. O datum exibido é o nível médio do mar do modelo.
- A **janela de pesca** é uma heurística explicável (solunar + maré + vento + chuva + luz do dia +
  pressão), com nota 1–5 e motivos. Não é um modelo treinado nem uma promessa.
- **Cache em memória** de 20 minutos por coordenada arredondada. Sem chave de API.
- **`/condicoes` é pública**. Salvar local e comparação exigem conta. O preenchimento automático e o
  rascunho local funcionam sem conta; a conta só é pedida ao salvar (revisado pela
  [ADR 0014](0014-registro-anonimo.md)).
- O **registro é pré-preenchido** com local, data, período, maré, lua e clima a partir do link de
  condições.
- A **comparação com o histórico** usa lua, maré, clima e período. Exige pelo menos 3 pescarias com
  captura e devolve um estado honesto quando não há padrão suficiente.
- Locais salvos são privados; a página pública não expõe a conta de quem consulta.

## Consequências

- O Fisga passa a ter um motivo diário para abrir o app, sem cobrar por isso.
- O pescador economiza os campos que mais dava preguiça de preencher.
- A maré pode errar horário/altura em relação à tábua oficial; o rótulo protege a confiança.
- Dependemos de dois provedores gratuitos. Se um falhar, a página ainda mostra o que estiver
  disponível e um aviso; sem maré, o solunar e o tempo continuam.
- O cache é local à instância; múltiplas réplicas exigem cache distribuído.
- A próxima evolução é importar a tábua oficial do CHM para os 44 portos, com atribuição e
  validação, e só então oferecer maré “oficial”.
- A janela precisa de validação com pescadores e comparação com o histórico real antes de ganhar
  mais peso na decisão.
