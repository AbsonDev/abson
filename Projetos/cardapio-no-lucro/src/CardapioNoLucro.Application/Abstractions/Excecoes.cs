namespace CardapioNoLucro.Application.Abstractions;

public sealed class RecursoNaoEncontradoException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}

public sealed class AcessoNegadoException(string mensagem) : Exception(mensagem);
