using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;

namespace Fisga.Api.Tests;

/// <summary>
/// HybridCache determinístico para a corrida em que a factory termina, grava o
/// valor e o chamador que a executou é cancelado antes de aplicar o TTL curto.
/// O observador recebe o valor devolvido e precisa corrigir a entrada sozinho.
/// </summary>
internal sealed class CacheDeCorrida : HybridCache
{
    private readonly string _chaveQueCancela;
    private readonly ConcurrentDictionary<string, Entrada> _itens = new();
    private readonly ConcurrentQueue<Gravacao> _gravacoes = new();
    private readonly TaskCompletionSource _valorArmazenado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _liberar =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _bloqueou;

    public CacheDeCorrida(string chaveQueCancela) => _chaveQueCancela = chaveQueCancela;

    public Task ValorArmazenado => _valorArmazenado.Task;

    public IReadOnlyCollection<Gravacao> Gravacoes => _gravacoes;

    public void Liberar() => _liberar.TrySetResult();

    public TimeSpan? Ttl(string chave) =>
        _itens.TryGetValue(chave, out var entrada) ? entrada.Ttl : null;

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (key == _chaveQueCancela && Interlocked.Exchange(ref _bloqueou, 1) == 0)
        {
            // A factory recebe CancellationToken.None: o trabalho termina e é
            // gravado antes de o chamador ser cancelado, como no HybridCache real.
            var calculado = await factory(state, CancellationToken.None);
            _itens[key] = new Entrada(calculado, options?.Expiration);
            _valorArmazenado.TrySetResult();
            await _liberar.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return calculado;
        }

        if (_itens.TryGetValue(key, out var existente) && existente.Valor is T valor)
        {
            return valor;
        }

        var novo = await factory(state, cancellationToken);
        _itens[key] = new Entrada(novo, options?.Expiration);
        return novo;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _gravacoes.Enqueue(new Gravacao(key, options?.Expiration));
        _itens[key] = new Entrada(value, options?.Expiration);
        return default;
    }

    public override ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _itens.TryRemove(key, out _);
        return default;
    }

    public override ValueTask RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken = default) => default;

    internal sealed record Gravacao(string Chave, TimeSpan? Expiration);

    private sealed record Entrada(object? Valor, TimeSpan? Ttl);
}
