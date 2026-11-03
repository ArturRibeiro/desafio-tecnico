namespace Lancamentos.Application.UseCases.Comum.Imp;

/// <summary>
/// Passos comuns a todos os comandos sobre uma conta, na ordem exigida pelas specs:
/// bloquear a conta, verificar a titularidade, verificar a idempotência e só então aplicar as regras do Aggregate.
/// </summary>
public abstract class OperacaoNaConta(IUnitOfWork unidade, IContaRepository contas, ILancamentoRepository lancamentos, TimeProvider relogio)
{
    protected ILancamentoRepository Lancamentos => lancamentos;

    protected Task<Resultado<Lancamento>> ExecutarAsync(
        TitularId titular,
        ContaId contaId,
        ChaveIdempotencia chave,
        Func<Lancamento, bool> mesmoConteudo,
        Func<Conta, DateTimeOffset, CancellationToken, Task<Resultado<Lancamento>>> aplicar,
        CancellationToken cancelamento) =>
        unidade.ExecutarAsync(
            async ct =>
            {
                var conta = await contas.ObterParaAlteracaoAsync(contaId, ct).ConfigureAwait(false);

                // Conta alheia e conta inexistente são indistinguíveis, e isso vem antes da chave:
                // repetir a chave de outra pessoa não revela o resultado dela.
                if (conta is null || !conta.PertenceA(titular))
                {
                    return Resultado.Falhar<Lancamento>(CodigosDeFalha.ContaNaoEncontrada, "Conta não encontrada.");
                }

                var existente = await lancamentos.ObterPorChaveAsync(conta.Id, chave, ct).ConfigureAwait(false);
                if (existente is not null)
                {
                    return mesmoConteudo(existente)
                        ? Resultado.Ok(existente)
                        : Resultado.Falhar<Lancamento>(CodigosDeFalha.ChaveIdempotenciaConflitante, "A Idempotency-Key já foi usada com outro conteúdo.");
                }

                var resultado = await aplicar(conta, relogio.GetUtcNow(), ct).ConfigureAwait(false);
                if (resultado.Sucesso)
                {
                    lancamentos.Adicionar(resultado.Valor);
                }

                return resultado;
            },
            cancelamento);

    protected static Resultado<LancamentoResponse> Responder(Resultado<Lancamento> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.Falha is { } falha ? Resultado.Falhar<LancamentoResponse>(falha) : Resultado.Ok(LancamentoResponse.De(resultado.Valor));
    }

    /// <summary>
    /// A repetição só devolve o original se pedir a mesma coisa. A competência só é comparada quando informada,
    /// porque o padrão é "agora" e mudaria a cada tentativa.
    /// </summary>
    protected static bool MesmoConteudo(Lancamento existente, TipoLancamento tipo, RegistrarLancamentoCommand comando)
    {
        ArgumentNullException.ThrowIfNull(existente);
        ArgumentNullException.ThrowIfNull(comando);

        return existente.Tipo == tipo
            && existente.Valor == comando.Valor
            && existente.Descricao == comando.Descricao
            && (comando.DataCompetencia is not { } competencia || existente.DataCompetencia == Instantes.Normalizar(competencia));
    }
}
