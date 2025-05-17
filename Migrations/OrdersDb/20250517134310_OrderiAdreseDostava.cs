using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bazaar.Migrations.OrdersDb
{
    /// <inheritdoc />
    public partial class OrderiAdreseDostava : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AdminDelivery",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReadyAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AdminDelivery",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExpectedReadyAt",
                table: "Orders");
        }
    }
}
