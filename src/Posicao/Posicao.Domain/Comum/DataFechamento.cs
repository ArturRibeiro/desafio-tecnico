namespace Posicao.Domain.Comum;

/// <summary>Dia de um fechamento, sempre em UTC.</summary>
public readonly record struct DataFechamento(DateOnly Valor) : IComparable<DataFechamento>
{
    public static DataFechamento De(DateTimeOffset instante) => new(DateOnly.FromDateTime(instante.UtcDateTime));

    /// <summary>Primeiro instante do dia (00:00 UTC).</summary>
    public DateTimeOffset Inicio => new(Valor.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public int CompareTo(DataFechamento other) => Valor.CompareTo(other.Valor);

    public static bool operator <(DataFechamento a, DataFechamento b) => a.CompareTo(b) < 0;

    public static bool operator >(DataFechamento a, DataFechamento b) => a.CompareTo(b) > 0;

    public static bool operator <=(DataFechamento a, DataFechamento b) => a.CompareTo(b) <= 0;

    public static bool operator >=(DataFechamento a, DataFechamento b) => a.CompareTo(b) >= 0;

    public override string ToString() => Valor.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
