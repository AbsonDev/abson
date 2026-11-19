# Instruções para agentes de código

Leia `AGENTS.md` na raiz e `Projetos/fisga/AGENTS.md` antes de propor mudanças.
A documentação de desenvolvimento do Fisga está indexada em
`Projetos/fisga/docs/INDEX.md`.

Regras que não podem ser violadas:

- Nada de segredos em código, Markdown, workflow ou `.env` commitado.
- O check obrigatório do repositório é `gate`; não crie outro check obrigatório.
- Mudou comportamento, contrato, configuração ou operação? Atualize a documentação no mesmo PR.
- `docs/api/openapi.json` é gerado por `Projetos/fisga/scripts/gerar-openapi.sh`; não edite à mão.
