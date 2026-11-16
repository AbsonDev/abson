namespace Precifica.Application.Abstractions;

public sealed class RecursoNaoEncontradoException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}

public sealed class AcessoNegadoException(string mensagem) : Exception(mensagem);

public sealed class PagamentoNecessarioException(string mensagem) : Exception(mensagem);

public sealed class IntegracaoIndisponivelException(string mensagem) : Exception(mensagem);
