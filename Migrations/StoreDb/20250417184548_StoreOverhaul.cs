using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bazaar.Migrations.StoreDb
{
    /// <inheritdoc />
    public partial class StoreOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "Stores",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Regions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Places",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Places");
        }
    }
}
