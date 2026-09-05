using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OroBI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialProductCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "CommercialMovements",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "CommercialMovements");
        }
    }
}
