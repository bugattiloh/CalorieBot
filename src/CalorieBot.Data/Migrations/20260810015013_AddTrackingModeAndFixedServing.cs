using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingModeAndFixedServing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrackingMode",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFixedServing",
                table: "FavoriteProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackingMode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsFixedServing",
                table: "FavoriteProducts");
        }
    }
}
