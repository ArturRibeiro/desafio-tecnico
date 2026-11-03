namespace Lancamentos.Application.UseCases.Writings.RegistrarCredito.Imp;

public sealed class RegistrarCreditoHandler(IUnitOfWork unidade, IContaRepository contas, ILancamentoRepository lancamentos, TimeProvider relogio)
    : OperacaoNaConta(unidade, contas, lancamentos, relogio), IRequestHandler<RegistrarCreditoCommand, Resultado<LancamentoResponse>>
{
    public async Task<Resultado<LancamentoResponse>> HandleAsync(RegistrarCreditoCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var resultado = await ExecutarAsync(
            comando.Titular,
            comando.ContaId,
            comando.Chave,
            existente => MesmoConteudo(existente, TipoLancamento.Credito, comando),
            (conta, agora, _) => Task.FromResult(conta.RegistrarCredito(comando.Valor, comando.Descricao, comando.DataCompetencia, comando.Chave, agora)),
            cancelamento).ConfigureAwait(false);
        return Responder(resultado);
    }
}
