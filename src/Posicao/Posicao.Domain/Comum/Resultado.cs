namespace Posicao.Domain.Comum;

public static class Resultado
{
    public static Resultado<T> Ok<T>(T valor) => new(valor);

    public static Resultado<T> Falhar<T>(string codigo, string mensagem) => new(new Falha(codigo, mensagem));

    public static Resultado<T> Falhar<T>(Falha falha) => new(falha);
}
