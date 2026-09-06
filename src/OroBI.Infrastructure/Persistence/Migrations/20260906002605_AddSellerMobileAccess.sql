START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906002605_AddSellerMobileAccess') THEN
    ALTER TABLE "Sellers" ADD "ExternalId" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906002605_AddSellerMobileAccess') THEN
    ALTER TABLE "AspNetUsers" ADD "LastLoginAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906002605_AddSellerMobileAccess') THEN
    ALTER TABLE "AspNetUsers" ADD "MustChangePassword" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906002605_AddSellerMobileAccess') THEN
    CREATE UNIQUE INDEX "IX_Sellers_ExternalId" ON "Sellers" ("ExternalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906002605_AddSellerMobileAccess') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906002605_AddSellerMobileAccess', '10.0.11');
    END IF;
END $EF$;
COMMIT;
