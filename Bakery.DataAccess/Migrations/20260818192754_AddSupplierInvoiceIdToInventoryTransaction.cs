using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bakery.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceIdToInventoryTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierInvoiceId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SupplierInvoiceId",
                table: "InventoryTransactions",
                column: "SupplierInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_SupplierInvoices_SupplierInvoiceId",
                table: "InventoryTransactions",
                column: "SupplierInvoiceId",
                principalTable: "SupplierInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_SupplierInvoices_SupplierInvoiceId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_SupplierInvoiceId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceId",
                table: "InventoryTransactions");
        }
    }
}
