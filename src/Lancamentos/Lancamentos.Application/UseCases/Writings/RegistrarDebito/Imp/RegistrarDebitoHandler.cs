namespace Lancamentos.Application.UseCases.Writings.RegistrarDebito.Imp;

public sealed class RegistrarDebitoHandler(IUnitOfWork unidade, IContaRepository contas, ILancamentoRepository lancamentos, TimeProvider relogio)
    : OperacaoNaConta(unidade, contas, lancamentos, relogio), IRequestHandler<RegistrarDebitoCommand, Resultado<LancamentoResponse>>
{
    public async Task<Resultado<LancamentoResponse>> HandleAsync(RegistrarDebitoCommand comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var resultado = await ExecutarAsync(
            comando.Titular,
            comando.ContaId,
            comando.Chave,
            existente => MesmoConteudo(existente, TipoLancamento.Debito, comando),
            (conta, agora, _) => Task.FromResult(conta.RegistrarDebito(comando.Valor, comando.Descricao, comando.DataCompetencia, comando.Chave, agora)),
            cancelamento).ConfigureAwait(false);
        return Responder(resultado);
    }
}
