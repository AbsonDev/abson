using System.Collections.Concurrent;

namespace Fisga.Application.Condicoes;

public interface ICacheDeCondicoes
{
    bool TentarObter(string chave, out DadosDeCondicoes? dados);

    void Guardar(string chave, DadosDeCondicoes dados, TimeSpan validade);
}

public sealed class CacheEmMemoriaDeCondicoes : ICacheDeCondicoes
{
    private readonly ConcurrentDictionary<string, Entrada> _itens = new(StringComparer.Ordinal);

    public bool TentarObter(string chave, out DadosDeCondicoes? dados)
    {
        if (_itens.TryGetValue(chave, out var entrada) && entrada.ExpiraEm > DateTimeOffset.UtcNow)
        {
            dados = entrada.Dados;
            return true;
        }

        _itens.TryRemove(chave, out _);
        dados = null;
        return false;
    }

    public void Guardar(string chave, DadosDeCondicoes dados, TimeSpan validade)
    {
        _itens[chave] = new Entrada(dados, DateTimeOffset.UtcNow.Add(validade));

        if (_itens.Count > 500)
        {
            var agora = DateTimeOffset.UtcNow;
            foreach (var par in _itens.Where(par => par.Value.ExpiraEm <= agora))
            {
                _itens.TryRemove(par.Key, out _);
            }
        }
    }

    private sealed record Entrada(DadosDeCondicoes Dados, DateTimeOffset ExpiraEm);
}
