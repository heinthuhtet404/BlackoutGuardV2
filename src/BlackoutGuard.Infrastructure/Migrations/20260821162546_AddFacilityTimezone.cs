using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackoutGuard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GeneratorCapacityKW",
                table: "facilities",
                newName: "GeneratorCapacityKw");

            migrationBuilder.AddColumn<string>(
                name: "TimezoneId",
                table: "facilities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimezoneId",
                table: "facilities");

            migrationBuilder.RenameColumn(
                name: "GeneratorCapacityKw",
                table: "facilities",
                newName: "GeneratorCapacityKW");
        }
    }
}
