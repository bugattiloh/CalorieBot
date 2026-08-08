using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CalorieBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    DailyCalorieLimit = table.Column<int>(type: "integer", nullable: false, defaultValue: 2000),
                    DailyProteinsLimit = table.Column<decimal>(type: "numeric(7,2)", nullable: true),
                    DailyFatsLimit = table.Column<decimal>(type: "numeric(7,2)", nullable: true),
                    DailyCarbsLimit = table.Column<decimal>(type: "numeric(7,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    GoalSetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: false),
                    Proteins = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Fats = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Carbs = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    ServingSize = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteProducts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: false),
                    Proteins = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Fats = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Carbs = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    ServingSize = table.Column<string>(type: "text", nullable: true),
                    MealType = table.Column<int>(type: "integer", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FavoriteProductId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodLog_FavoriteProducts_FavoriteProductId",
                        column: x => x.FavoriteProductId,
                        principalTable: "FavoriteProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoodLog_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteProducts_UserId_Name",
                table: "FavoriteProducts",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodLog_FavoriteProductId",
                table: "FoodLog",
                column: "FavoriteProductId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLog_UserId_LoggedAt",
                table: "FoodLog",
                columns: new[] { "UserId", "LoggedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoodLog");

            migrationBuilder.DropTable(
                name: "FavoriteProducts");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
