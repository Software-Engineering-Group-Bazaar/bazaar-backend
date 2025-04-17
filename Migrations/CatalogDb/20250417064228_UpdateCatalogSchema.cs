using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bazaar.Migrations.CatalogDb
{
    /// <inheritdoc />
    public partial class UpdateCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WholesaleThreshold",
                table: "Products",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WholesaleThreshold",
                table: "Products");
        }
    }
}
