#nullable disable

namespace Posicao.Infrastructure.Migracoes
{
    /// <inheritdoc />
    public partial class ConciliacaoComLancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "saldo_informado_apos",
                table: "movimentos",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "saldo_informado_apos",
                table: "movimentos");
        }
    }
}
