using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bazaar.Migrations.CatalogDb
{
    /// <inheritdoc />
    public partial class pointRateUProduktu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PointRate",
                table: "Products",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointRate",
                table: "Products");
        }
    }
}
