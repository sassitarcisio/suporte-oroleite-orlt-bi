using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OroBI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateImportedClosingDefaultsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportedClosingDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PppMaximumAward = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SellerSalariesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedClosingDefaults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedClosingDefaults_ImportBatchId",
                table: "ImportedClosingDefaults",
                column: "ImportBatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedClosingDefaults");
        }
    }
}
