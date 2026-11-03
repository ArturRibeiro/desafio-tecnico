namespace Posicao.Application.Ports;

/// <summary>
/// Registra, para análise, uma divergência entre o saldo calculado pela Posição e o informado por Lançamentos
/// (DOMINIO.md, seção 6.8). Não altera a posição nem interrompe a projeção.
/// </summary>
public interface IRegistroDeDivergencias
{
    void Registrar(Divergencia divergencia);
}
