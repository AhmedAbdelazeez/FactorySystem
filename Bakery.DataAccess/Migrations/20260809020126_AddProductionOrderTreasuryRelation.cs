using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bakery.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderTreasuryRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_ProductionOrderId",
                table: "TreasuryTransactions",
                column: "ProductionOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransactions_ProductionOrders_ProductionOrderId",
                table: "TreasuryTransactions",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransactions_ProductionOrders_ProductionOrderId",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_ProductionOrderId",
                table: "TreasuryTransactions");
        }
    }
}
