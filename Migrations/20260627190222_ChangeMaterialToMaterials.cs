using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gold_e_Shop.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMaterialToMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Materials",
                table: "products",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.Sql("UPDATE products SET \"Materials\" = ARRAY[\"Material\"] WHERE \"Material\" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Materials",
                table: "products");

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "products",
                type: "text",
                nullable: true);
        }
    }
}
