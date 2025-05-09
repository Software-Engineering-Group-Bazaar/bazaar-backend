using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace bazaar.Migrations.AdDb
{
    /// <inheritdoc />
    public partial class AktivistickiKorisnici : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Advertisments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SellerId = table.Column<string>(type: "text", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    ViewPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Clicks = table.Column<int>(type: "integer", nullable: false),
                    ClickPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Conversions = table.Column<int>(type: "integer", nullable: false),
                    ConversionPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AdType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advertisments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ProductCategoryId = table.Column<int>(type: "integer", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InteractionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AdvertismentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdData_Advertisments_AdvertismentId",
                        column: x => x.AdvertismentId,
                        principalTable: "Advertisments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdvertismentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clicks_Advertisments_AdvertismentId",
                        column: x => x.AdvertismentId,
                        principalTable: "Advertisments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdvertismentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversions_Advertisments_AdvertismentId",
                        column: x => x.AdvertismentId,
                        principalTable: "Advertisments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Views",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdvertismentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Views", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Views_Advertisments_AdvertismentId",
                        column: x => x.AdvertismentId,
                        principalTable: "Advertisments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdData_AdvertismentId",
                table: "AdData",
                column: "AdvertismentId");

            migrationBuilder.CreateIndex(
                name: "IX_Clicks_AdvertismentId",
                table: "Clicks",
                column: "AdvertismentId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_AdvertismentId",
                table: "Conversions",
                column: "AdvertismentId");

            migrationBuilder.CreateIndex(
                name: "IX_Views_AdvertismentId",
                table: "Views",
                column: "AdvertismentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdData");

            migrationBuilder.DropTable(
                name: "Clicks");

            migrationBuilder.DropTable(
                name: "Conversions");

            migrationBuilder.DropTable(
                name: "UserActivities");

            migrationBuilder.DropTable(
                name: "Views");

            migrationBuilder.DropTable(
                name: "Advertisments");
        }
    }
}
