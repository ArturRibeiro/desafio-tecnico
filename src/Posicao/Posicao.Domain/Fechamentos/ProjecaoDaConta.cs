namespace Posicao.Domain.Fechamentos;

/// <summary>
/// Domain Service que aplica os movimentos pendentes de uma conta aos fechamentos (DOMINIO.md, seções 6.1 e 6.2).
/// O MassTransit não garante ordem: a ordem vem da sequência, e a aplicação para na primeira lacuna.
/// </summary>
public static class ProjecaoDaConta
{
    /// <param name="ultimoFechamento">Fechamento mais recente da conta, ou nulo se ainda não houver.</param>
    /// <param name="pendentes">Movimentos recebidos e ainda não aplicados, em qualquer ordem.</param>
    /// <param name="originais">Movimentos já aplicados que algum estorno pendente anula.</param>
    public static ResultadoDaProjecao Aplicar(
        Titularidade titularidade,
        FechamentoDiario? ultimoFechamento,
        IEnumerable<Movimento> pendentes,
        IEnumerable<Movimento> originais)
    {
        ArgumentNullException.ThrowIfNull(titularidade);
        ArgumentNullException.ThrowIfNull(pendentes);
        ArgumentNullException.ThrowIfNull(originais);

        var porLancamento = originais.Where(m => m.Efeito is not null).ToDictionary(m => m.LancamentoId);
        var aplicados = new List<Movimento>();
        var novos = new List<FechamentoDiario>();
        var divergencias = new List<Divergencia>();
        var atual = ultimoFechamento;

        var ordenados = pendentes
            .Where(m => m.ContaId == titularidade.ContaId && m.Sequencia > titularidade.UltimaSequenciaAplicada)
            .DistinctBy(m => m.Sequencia)
            .OrderBy(m => m.Sequencia);
        foreach (var movimento in ordenados)
        {
            if (movimento.Sequencia != titularidade.UltimaSequenciaAplicada + 1)
            {
                break;
            }

            var dia = DataFechamento.De(movimento.DataCompetencia);
            if (atual is not null && dia < atual.Data)
            {
                return Interrompido(aplicados, novos, divergencias, new Falha(
                    CodigosDeFalha.CompetenciaForaDeOrdem,
                    $"A sequência {movimento.Sequencia} tem competência em {dia}, antes do último fechamento ({atual.Data})."));
            }

            var efeito = EfeitoDe(movimento, porLancamento);
            if (efeito is null)
            {
                return Interrompido(aplicados, novos, divergencias, new Falha(
                    CodigosDeFalha.EstornoSemOriginal,
                    $"O estorno de sequência {movimento.Sequencia} anula um lançamento que não foi aplicado."));
            }

            movimento.DefinirEfeito(efeito.Value);
            if (atual is null || dia > atual.Data)
            {
                atual = FechamentoDiario.Abrir(titularidade.ContaId, dia, atual?.SaldoFechamento ?? 0m);
                novos.Add(atual);
            }

            atual.Aplicar(movimento);
            if (movimento.SaldoInformadoApos is { } informado && informado != atual.SaldoFechamento)
            {
                divergencias.Add(new Divergencia(movimento.ContaId, movimento.Sequencia, movimento.LancamentoId));
            }

            titularidade.Avancar(movimento);
            porLancamento[movimento.LancamentoId] = movimento;
            aplicados.Add(movimento);
        }

        return new(aplicados, novos, null, divergencias);
    }

    /// <summary>
    /// Recalcula a conta do zero a partir de todos os movimentos recebidos (DOMINIO.md, seção 6.6). Os fechamentos
    /// antigos devem ser descartados por quem chama; os novos vêm em <see cref="ResultadoDaProjecao.NovosFechamentos"/>.
    /// </summary>
    public static ResultadoDaProjecao Reconstruir(Titularidade titularidade, IReadOnlyCollection<Movimento> todos)
    {
        ArgumentNullException.ThrowIfNull(titularidade);
        ArgumentNullException.ThrowIfNull(todos);

        titularidade.Reiniciar();
        foreach (var movimento in todos)
        {
            movimento.Reiniciar();
        }

        return Aplicar(titularidade, null, todos, []);
    }

    private static ResultadoDaProjecao Interrompido(
        List<Movimento> aplicados, List<FechamentoDiario> novos, List<Divergencia> divergencias, Falha motivo) =>
        new(aplicados, novos, motivo, divergencias);

    private static Efeito? EfeitoDe(Movimento movimento, Dictionary<LancamentoId, Movimento> porLancamento) => movimento.Tipo switch
    {
        TipoMovimento.Credito => Efeito.Credito,
        TipoMovimento.Debito => Efeito.Debito,
        _ => porLancamento.TryGetValue(movimento.LancamentoEstornadoId!.Value, out var original)
            ? original.Efeito == Efeito.Credito ? Efeito.Debito : Efeito.Credito
            : null,
    };
}
