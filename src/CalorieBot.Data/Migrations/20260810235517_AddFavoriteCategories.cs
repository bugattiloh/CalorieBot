using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CalorieBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryKind",
                table: "FavoriteProducts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductCategoryId",
                table: "FavoriteProducts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DishIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DishFavoriteProductId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Proteins = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Fats = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Carbs = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Calories = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DishIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DishIngredients_FavoriteProducts_DishFavoriteProductId",
                        column: x => x.DishFavoriteProductId,
                        principalTable: "FavoriteProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteProducts_ProductCategoryId",
                table: "FavoriteProducts",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DishIngredients_DishFavoriteProductId",
                table: "DishIngredients",
                column: "DishFavoriteProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_UserId_Name",
                table: "ProductCategories",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteProducts_ProductCategories_ProductCategoryId",
                table: "FavoriteProducts",
                column: "ProductCategoryId",
                principalTable: "ProductCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteProducts_ProductCategories_ProductCategoryId",
                table: "FavoriteProducts");

            migrationBuilder.DropTable(
                name: "DishIngredients");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteProducts_ProductCategoryId",
                table: "FavoriteProducts");

            migrationBuilder.DropColumn(
                name: "CategoryKind",
                table: "FavoriteProducts");

            migrationBuilder.DropColumn(
                name: "ProductCategoryId",
                table: "FavoriteProducts");
        }
    }
}
