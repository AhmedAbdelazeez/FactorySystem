using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bakery.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMatrialTypeLoookUpTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "RawMaterials");

            migrationBuilder.AddColumn<int>(
                name: "MaterialTypeId",
                table: "RawMaterials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MaterialTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MaterialTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "شكاره دقيق" },
                    { 2, "خميرة" },
                    { 3, "مواد حافظه" },
                    { 4, "سكر" },
                    { 5, "زيت" },
                    { 6, "زبده" },
                    { 7, "ورق تغليف" },
                    { 8, "محسن" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_MaterialTypeId",
                table: "RawMaterials",
                column: "MaterialTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RawMaterials_MaterialTypes_MaterialTypeId",
                table: "RawMaterials",
                column: "MaterialTypeId",
                principalTable: "MaterialTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RawMaterials_MaterialTypes_MaterialTypeId",
                table: "RawMaterials");

            migrationBuilder.DropTable(
                name: "MaterialTypes");

            migrationBuilder.DropIndex(
                name: "IX_RawMaterials_MaterialTypeId",
                table: "RawMaterials");

            migrationBuilder.DropColumn(
                name: "MaterialTypeId",
                table: "RawMaterials");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "RawMaterials",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }
    }
}
