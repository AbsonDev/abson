namespace Fisga.Application.Newsletter;

public static class ConteudoSazonal
{
    public static IReadOnlyList<LeituraSazonal> ParaMes(int mes) => mes switch
    {
        12 or 1 or 2 =>
        [
            new("Tucunaré em água quente", "/wiki/especies/tucunare", "Estrutura, sombra e as primeiras horas do dia."),
            new("Dourado na correnteza", "/wiki/especies/dourado", "Onde procurar e como trabalhar o artificial."),
            new("Robalo no estuário", "/wiki/especies/robalo", "Maré, estrutura e apresentação."),
        ],
        3 or 4 or 5 =>
        [
            new("Tainha na costa", "/wiki/especies/tainha", "A época da migração e a leitura da praia."),
            new("Robalo no estuário", "/wiki/especies/robalo", "Água em movimento e estrutura."),
            new("Corvina no fundo", "/wiki/especies/corvina", "Isca natural e paciência na praia."),
        ],
        6 or 7 or 8 =>
        [
            new("Tainha no inverno", "/wiki/especies/tainha", "Cardumes, maré e arremesso longo."),
            new("Truta na serra", "/wiki/especies/truta", "Água fria, equipamento leve e apresentação delicada."),
            new("Pesca de fundo", "/wiki/tecnicas/pesca-de-fundo", "A técnica mais democrática para peixes de fundo."),
        ],
        _ =>
        [
            new("Tucunaré na primavera", "/wiki/especies/tucunare", "Água mais quente e peixe ativo."),
            new("Traíra na superfície", "/wiki/especies/traira", "Popper, margem e pausas."),
            new("Pacu na cheia", "/wiki/especies/pacu", "Frutos, sementes e fundo."),
        ],
    };
}
