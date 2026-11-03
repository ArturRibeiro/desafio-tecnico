#nullable disable

namespace Posicao.Infrastructure.Migracoes
{
    /// <summary>
    /// Função <c>bloquear_conta</c>, consultada em LINQ pelo TitularidadeRepository: faz <c>pg_advisory_xact_lock</c>
    /// sobre a conta (liberado no fim da transação) e devolve a conta bloqueada. A chave é a mesma usada antes,
    /// <c>hashtextextended</c> do identificador em texto, então instâncias antigas e novas disputam o mesmo lock.
    /// </summary>
    public partial class ConsultaDeBloqueioDaConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION bloquear_conta(conta uuid) RETURNS TABLE (conta_id uuid)
                LANGUAGE sql VOLATILE AS $$
                    SELECT conta FROM (SELECT pg_advisory_xact_lock(hashtextextended(conta::text, 0))) AS bloqueio
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION bloquear_conta(uuid);");
        }
    }
}
