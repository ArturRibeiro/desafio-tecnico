namespace Lancamentos.Domain.Comum;

/// <summary>
/// Falha de negócio esperada. O <see cref="Codigo"/> é estável e chega ao cliente no campo "codigo" das respostas de erro.
/// </summary>
public sealed record Falha(string Codigo, string Mensagem);
