using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Product.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxEventTraceParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                table: "product_outbox_events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_parent",
                table: "product_outbox_events");
        }
    }
}
