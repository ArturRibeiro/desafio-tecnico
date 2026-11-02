namespace Lancamentos.Application.UseCases.AbrirConta.Imp;

public sealed record AbrirContaCommand(TitularId Titular, ChaveIdempotencia Chave) : IRequest<Resultado<ContaResponse>>;
