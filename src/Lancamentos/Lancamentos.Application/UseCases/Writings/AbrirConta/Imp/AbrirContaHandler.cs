namespace Lancamentos.Application.UseCases.Writings.AbrirConta.Imp;

/// <summary>Abre uma conta para o titular autenticado; repetir a mesma chave devolve a conta já aberta.</summary>
public sealed class AbrirContaHandler(IUnitOfWork unidade, IContaRepository contas, TimeProvider relogio)
    : IRequestHandler<AbrirContaCommand, Resultado<ContaResponse>>
{
    public async Task<Resultado<ContaResponse>> HandleAsync(AbrirContaCommand comando, CancellationToken cancelamento)
    {
        var resultado = await AbrirAsync(comando, cancelamento).ConfigureAwait(false);
        return resultado.Falha is { } falha ? Resultado.Falhar<ContaResponse>(falha) : Resultado.Ok(ContaResponse.De(resultado.Valor));
    }

    private async Task<Resultado<Conta>> AbrirAsync(AbrirContaCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var resultado = await unidade.ExecutarAsync(
            async ct =>
            {
                var existente = await contas.ObterPorChaveDeAberturaAsync(comando.Titular, comando.Chave, ct).ConfigureAwait(false);
                if (existente is not null)
                {
                    return Resultado.Ok(existente);
                }

                var conta = Conta.Abrir(ContaId.Novo(), comando.Titular, comando.Chave, relogio.GetUtcNow());
                contas.Adicionar(conta);
                return Resultado.Ok(conta);
            },
            cancelamento).ConfigureAwait(false);

        // Duas aberturas simultâneas com a mesma chave: o índice único barrou a segunda; devolve a que venceu.
        if (resultado.Falha?.Codigo == CodigosDeFalhaTecnica.AberturaConcorrente)
        {
            var vencedora = await contas.ObterPorChaveDeAberturaAsync(comando.Titular, comando.Chave, cancelamento).ConfigureAwait(false);
            return vencedora is null ? resultado : Resultado.Ok(vencedora);
        }

        return resultado;
    }
}
