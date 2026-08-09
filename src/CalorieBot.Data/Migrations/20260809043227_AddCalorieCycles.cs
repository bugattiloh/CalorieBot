using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CalorieBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalorieCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CycleStartedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "CalorieCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalorieLimit = table.Column<int>(type: "integer", nullable: false),
                    ConsumedCalories = table.Column<int>(type: "integer", nullable: false),
                    Proteins = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Fats = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    Carbs = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 0m),
                    EntriesCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalorieCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalorieCycles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalorieCycles_UserId_EndedAt",
                table: "CalorieCycles",
                columns: new[] { "UserId", "EndedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalorieCycles");

            migrationBuilder.DropColumn(
                name: "CycleStartedAt",
                table: "Users");
        }
    }
}
