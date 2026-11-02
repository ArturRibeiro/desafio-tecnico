namespace Lancamentos.Application.UseCases.EstornarLancamento.Imp;

public sealed class EstornarLancamentoHandler(IUnidadeDeTrabalho unidade, IRepositorioDeContas contas, IRepositorioDeLancamentos lancamentos, TimeProvider relogio)
    : OperacaoNaConta(unidade, contas, lancamentos, relogio), IRequestHandler<EstornarLancamentoCommand, Resultado<LancamentoResponse>>
{
    public async Task<Resultado<LancamentoResponse>> HandleAsync(EstornarLancamentoCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var resultado = await ExecutarAsync(
            comando.Titular,
            comando.ContaId,
            comando.Chave,
            existente => existente.Tipo == TipoLancamento.Estorno
                && existente.LancamentoEstornadoId == comando.LancamentoId
                && (comando.Descricao is null || existente.Descricao == comando.Descricao),
            async (conta, agora, ct) =>
            {
                var original = await Lancamentos.ObterAsync(conta.Id, comando.LancamentoId, ct).ConfigureAwait(false);
                if (original is null)
                {
                    return Resultado.Falhar<Lancamento>(CodigosDeFalha.LancamentoNaoEncontrado, "Lançamento não encontrado nesta conta.");
                }

                var jaEstornado = await Lancamentos.ExisteEstornoDeAsync(original.Id, ct).ConfigureAwait(false);
                return conta.Estornar(original, jaEstornado, comando.Descricao, comando.Chave, agora);
            },
            cancelamento).ConfigureAwait(false);
        return Responder(resultado);
    }
}
