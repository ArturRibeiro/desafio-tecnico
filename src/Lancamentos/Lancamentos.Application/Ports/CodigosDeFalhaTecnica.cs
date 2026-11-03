namespace Lancamentos.Application.Ports;

/// <summary>Falhas técnicas que a Infraestrutura traduz em código, para a Aplicação poder reagir.</summary>
public static class CodigosDeFalhaTecnica
{
    /// <summary>Outra abertura com a mesma chave e o mesmo titular foi confirmada antes (índice único).</summary>
    public const string AberturaConcorrente = "abertura-concorrente";
}
