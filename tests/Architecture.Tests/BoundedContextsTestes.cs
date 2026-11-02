namespace Architecture.Tests;

/// <summary>
/// Specs "Bounded Contexts independentes", "Biblioteca de hospedagem sem conceitos de domínio"
/// e "Contratos de integração sem dependências".
/// </summary>
public sealed class BoundedContextsTestes
{
    private const string Contratos = "Contratos.Integracao";
    private const string Plataforma = "Plataforma.Hospedagem";

    public static TheoryData<string, string> ParesDeContextos => new()
    {
        { "Lancamentos", "Posicao" },
        { "Posicao", "Lancamentos" },
    };

    [Theory(DisplayName = "Projetos de um bounded context não referenciam projetos do outro contexto")]
    [MemberData(nameof(ParesDeContextos))]
    public void ProjetosDeUmContexto_NaoReferenciamOutroContexto(string contexto, string outro)
    {
        var projetosDoOutro = Solucao.Projetos.Where(p => p.Pasta == outro).Select(p => p.Nome).ToHashSet();

        Solucao.Projetos.Where(p => p.Pasta == contexto).ToList().ForEach(projeto => projeto.Referencias.Should().NotContain(referencia => projetosDoOutro.Contains(referencia)));
    }

    [Theory(DisplayName = "Tipos de um bounded context não dependem de tipos do outro contexto")]
    [MemberData(nameof(ParesDeContextos))]
    public void TiposDeUmContexto_NaoDependemDeOutroContexto(string contexto, string outro)
    {
        Solucao.Projetos.Where(p => p.Pasta == contexto).ToList().ForEach(projeto =>
            {
                var resultado = Types.InAssembly(Solucao.Assembly(projeto.Nome))
                    .ShouldNot().HaveDependencyOn(outro)
                    .GetResult();
                resultado.IsSuccessful.Should().BeTrue(CamadasTestes.Descrever(resultado));
            });
    }

    [Fact(DisplayName = "Projetos de um contexto só referenciam o próprio contexto ou os projetos compartilhados")]
    public void ProjetosForaDeCompartilhado_SoReferenciamOProprioContextoOuCompartilhados()
    {
        var compartilhados = Solucao.Projetos.Where(p => p.Pasta == "Compartilhado").Select(p => p.Nome).ToHashSet();

        Solucao.Projetos.Where(p => p.Pasta != "Compartilhado").ToList().ForEach(projeto => projeto.Referencias.ToList().ForEach(r => (compartilhados.Contains(r) || Solucao.Projeto(r).Pasta == projeto.Pasta).Should().BeTrue($"{projeto.Nome} referencia {r}, de outro contexto.")));
    }

    [Fact(DisplayName = "Plataforma.Hospedagem só é usada pela borda (Api, Worker e Gateway)")]
    public void PlataformaHospedagem_SoEhUsadaPelaBorda()
    {
        var usuarios = Solucao.Projetos.Where(p => p.Referencias.Contains(Plataforma));

        usuarios.ToList().ForEach(p => p.EhBorda.Should().BeTrue($"{p.Nome} não é Api, Worker nem Gateway e referencia {Plataforma}."));
    }

    [Fact(DisplayName = "Plataforma.Hospedagem não referencia contextos nem contratos de integração")]
    public void PlataformaHospedagem_NaoReferenciaContextosNemContratos()
    {
        // Só o Mediator, que também é técnico: a borda registra os handlers por ele.
        Solucao.Projeto(Plataforma).Referencias.ToList().ForEach(r => r.Should().Be("Plataforma.Mediator"));

        var resultado = Types.InAssembly(Solucao.Assembly(Plataforma))
            .ShouldNot().HaveDependencyOnAny("Lancamentos", "Posicao", Contratos)
            .GetResult();
        resultado.IsSuccessful.Should().BeTrue(CamadasTestes.Descrever(resultado));
    }

    /// <summary>O Mediator é compartilhado pelas Applications, então não pode trazer dependência nenhuma para elas.</summary>
    [Fact(DisplayName = "Plataforma.Mediator não referencia projetos nem pacotes")]
    public void PlataformaMediator_NaoReferenciaProjetosNemPacotes()
    {
        var mediator = Solucao.Projeto("Plataforma.Mediator");

        mediator.Referencias.Should().BeEmpty();
        mediator.Pacotes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Contratos de integração não referenciam projetos nem pacotes")]
    public void ContratosDeIntegracao_NaoReferenciamProjetosNemPacotes()
    {
        var contratos = Solucao.Projeto(Contratos);

        contratos.Referencias.Should().BeEmpty();
        contratos.Pacotes.Should().BeEmpty();
    }
}
