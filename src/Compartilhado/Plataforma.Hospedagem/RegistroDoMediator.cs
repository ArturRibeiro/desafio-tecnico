namespace Plataforma.Hospedagem;

/// <summary>Registra o Mediator e todos os handlers de um assembly de Application, um para cada tipo de pedido.</summary>
public static class RegistroDoMediator
{
    public static IServiceCollection AdicionarMediator(this IServiceCollection servicos, Assembly casosDeUso)
    {
        ArgumentNullException.ThrowIfNull(casosDeUso);

        servicos.AddScoped<IMediator, ServiceProviderMediator>();

        var handlers = casosDeUso.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false })
            .SelectMany(tipo => tipo.GetInterfaces()
                .Where(contrato => contrato.IsGenericType && contrato.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(contrato => (Contrato: contrato, Tipo: tipo)));
        foreach (var (contrato, tipo) in handlers)
        {
            servicos.AddScoped(contrato, tipo);
        }

        return servicos;
    }
}
