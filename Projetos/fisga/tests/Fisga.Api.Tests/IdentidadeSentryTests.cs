using Fisga.Api.Observabilidade;

namespace Fisga.Api.Tests;

public sealed class IdentidadeSentryTests
{
    [Fact]
    public void ParaUsuario_gera_id_sem_outros_campos()
    {
        var id = Guid.NewGuid();

        var usuario = IdentidadeSentry.ParaUsuario(id);

        Assert.Equal(id.ToString(), usuario.Id);
        Assert.Null(usuario.Email);
        Assert.Null(usuario.Username);
        Assert.Null(usuario.IpAddress);
        Assert.Empty(usuario.Other);
    }
}
