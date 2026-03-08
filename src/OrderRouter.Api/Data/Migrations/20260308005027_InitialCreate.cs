using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OrderRouter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductCode = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    SatisfactionScore = table.Column<decimal>(type: "numeric", nullable: false),
                    CanMailOrder = table.Column<bool>(type: "boolean", nullable: false),
                    ServesNationwide = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProductCategories_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierServiceZips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Zip = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierServiceZips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierServiceZips_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCode",
                table: "Products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductCategories_Category",
                table: "SupplierProductCategories",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductCategories_SupplierId",
                table: "SupplierProductCategories",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierId",
                table: "Suppliers",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierServiceZips_SupplierId",
                table: "SupplierServiceZips",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierServiceZips_Zip",
                table: "SupplierServiceZips",
                column: "Zip");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SupplierProductCategories");

            migrationBuilder.DropTable(
                name: "SupplierServiceZips");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
