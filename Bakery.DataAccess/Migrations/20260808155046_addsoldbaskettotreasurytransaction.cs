using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bakery.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class addsoldbaskettotreasurytransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SoldBaskets",
                table: "TreasuryTransactions",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoldBaskets",
                table: "TreasuryTransactions");
        }
    }
}
