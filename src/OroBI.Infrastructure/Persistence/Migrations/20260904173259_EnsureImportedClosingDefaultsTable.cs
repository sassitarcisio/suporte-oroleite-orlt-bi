using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OroBI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureImportedClosingDefaultsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "ImportedClosingDefaults" (
                    "Id" uuid NOT NULL,
                    "ImportBatchId" uuid NOT NULL,
                    "BaseSalary" numeric(18,2) NULL,
                    "CommissionPercent" numeric(9,4) NULL,
                    "PppMaximumAward" numeric(18,2) NULL,
                    "SellerSalariesJson" jsonb NOT NULL DEFAULT '{}'::jsonb,
                    CONSTRAINT "PK_ImportedClosingDefaults" PRIMARY KEY ("Id")
                );

                ALTER TABLE "ImportedClosingDefaults" ADD COLUMN IF NOT EXISTS "ImportBatchId" uuid;
                ALTER TABLE "ImportedClosingDefaults" ADD COLUMN IF NOT EXISTS "BaseSalary" numeric(18,2);
                ALTER TABLE "ImportedClosingDefaults" ADD COLUMN IF NOT EXISTS "CommissionPercent" numeric(9,4);
                ALTER TABLE "ImportedClosingDefaults" ADD COLUMN IF NOT EXISTS "PppMaximumAward" numeric(18,2);
                ALTER TABLE "ImportedClosingDefaults" ADD COLUMN IF NOT EXISTS "SellerSalariesJson" jsonb NOT NULL DEFAULT '{}'::jsonb;
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ImportedClosingDefaults_ImportBatchId"
                    ON "ImportedClosingDefaults" ("ImportBatchId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recovery migration: imported closing defaults must not be deleted on rollback.
        }
    }
}
