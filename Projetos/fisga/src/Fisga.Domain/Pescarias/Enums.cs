namespace Fisga.Domain.Pescarias;

public enum AmbienteDePesca
{
    Rio = 1,
    Mar = 2,
    Lagoa = 3,
    Represa = 4,
    PesquePague = 5,
    Outro = 6
}

public enum PeriodoDoDia
{
    Madrugada = 1,
    Manha = 2,
    Tarde = 3,
    Noite = 4
}

public enum FaseDaMare
{
    NaoSeAplica = 0,
    Vazante = 1,
    Enchente = 2,
    Cheia = 3,
    Seca = 4
}

public enum FaseDaLua
{
    NaoInformada = 0,
    Nova = 1,
    Crescente = 2,
    Cheia = 3,
    Minguante = 4
}

public enum Clima
{
    NaoInformado = 0,
    Sol = 1,
    Nublado = 2,
    Chuva = 3,
    Vento = 4
}

public enum ClarezaDaAgua
{
    NaoInformada = 0,
    Clara = 1,
    Turva = 2,
    Barrenta = 3
}


public enum PrivacidadeDoLocal
{
    Exato = 0,
    Cidade = 1,
    Oculto = 2,
    Trecho = 3,
    CorpoAgua = 4
}
