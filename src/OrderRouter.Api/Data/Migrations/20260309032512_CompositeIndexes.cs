using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderRouter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierServiceZips_Zip",
                table: "SupplierServiceZips");

            migrationBuilder.DropIndex(
                name: "IX_SupplierProductCategories_Category",
                table: "SupplierProductCategories");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierServiceZips_Zip_SupplierId",
                table: "SupplierServiceZips",
                columns: new[] { "Zip", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductCategories_Category_SupplierId",
                table: "SupplierProductCategories",
                columns: new[] { "Category", "SupplierId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierServiceZips_Zip_SupplierId",
                table: "SupplierServiceZips");

            migrationBuilder.DropIndex(
                name: "IX_SupplierProductCategories_Category_SupplierId",
                table: "SupplierProductCategories");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierServiceZips_Zip",
                table: "SupplierServiceZips",
                column: "Zip");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductCategories_Category",
                table: "SupplierProductCategories",
                column: "Category");
        }
    }
}
