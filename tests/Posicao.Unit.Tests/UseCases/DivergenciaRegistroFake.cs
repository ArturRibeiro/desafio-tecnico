namespace Posicao.Unit.Tests.UseCases;

/// <summary>Porta de registro de divergências em memória: guarda o que foi registrado, em ordem.</summary>
internal sealed class DivergenciaRegistroFake : IRegistroDeDivergencias
{
    public List<Divergencia> Registradas { get; } = [];

    public void Registrar(Divergencia divergencia) => Registradas.Add(divergencia);
}
