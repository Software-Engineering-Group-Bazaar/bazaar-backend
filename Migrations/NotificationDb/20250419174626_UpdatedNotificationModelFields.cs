using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bazaar.Notification.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedNotificationModelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "RelatedEntityId",
                table: "Notifications",
                newName: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderId",
                table: "Notifications",
                newName: "RelatedEntityId");

            migrationBuilder.AddColumn<string>(
                name: "LinkUrl",
                table: "Notifications",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
