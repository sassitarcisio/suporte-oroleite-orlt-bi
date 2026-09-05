using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OroBI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegistrationPending",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationName",
                table: "AspNetUsers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRegistrationPending",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationName",
                table: "AspNetUsers");
        }
    }
}
