namespace Lancamentos.Application.Portas;

/// <summary>Dados da requisição em curso que acompanham os eventos publicados.</summary>
public interface IContextoDaRequisicao
{
    string CorrelacaoId { get; }
}
