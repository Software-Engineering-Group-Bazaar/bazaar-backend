using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace bazaar.Migrations.CatalogDb
{
    /// <inheritdoc />
    public partial class SlikeJbt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPicture_Products_ProductId",
                table: "ProductPicture");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPicture",
                table: "ProductPicture");

            migrationBuilder.RenameTable(
                name: "ProductPicture",
                newName: "ProductPictures");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPicture_ProductId",
                table: "ProductPictures",
                newName: "IX_ProductPictures_ProductId");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ProductPictures",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPictures",
                table: "ProductPictures",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPictures_Products_ProductId",
                table: "ProductPictures",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPictures_Products_ProductId",
                table: "ProductPictures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPictures",
                table: "ProductPictures");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ProductPictures");

            migrationBuilder.RenameTable(
                name: "ProductPictures",
                newName: "ProductPicture");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPictures_ProductId",
                table: "ProductPicture",
                newName: "IX_ProductPicture_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPicture",
                table: "ProductPicture",
                column: "Url");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPicture_Products_ProductId",
                table: "ProductPicture",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
