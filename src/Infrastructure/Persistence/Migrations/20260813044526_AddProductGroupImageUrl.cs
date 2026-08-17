using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Product.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroupImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "product_groups",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_url",
                table: "product_groups");
        }
    }
}
