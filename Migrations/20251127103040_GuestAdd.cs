using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_e_Shop.Migrations
{
    /// <inheritdoc />
    public partial class GuestAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "guests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "guests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HouseNumber",
                table: "guests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "guests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Surname",
                table: "guests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "HouseNumber",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "Surname",
                table: "guests");
        }
    }
}
