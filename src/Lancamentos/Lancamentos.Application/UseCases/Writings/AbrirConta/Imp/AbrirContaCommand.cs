namespace Lancamentos.Application.UseCases.Writings.AbrirConta.Imp;

public sealed record AbrirContaCommand(TitularId Titular, ChaveIdempotencia Chave) : IRequest<Resultado<ContaResponse>>;
