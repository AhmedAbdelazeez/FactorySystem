using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bakery.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedProductTypeToProductionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedProductType",
                table: "ProductionOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedProductType",
                table: "ProductionOrders");
        }
    }
}
