START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    ALTER TABLE "AspNetUsers" ADD "IsActive" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE TABLE "AccountAuditEvents" (
        "Id" uuid NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "ActorUserId" character varying(450) NOT NULL,
        "Action" character varying(80) NOT NULL,
        "TargetId" character varying(450) NOT NULL,
        "DetailsJson" text,
        CONSTRAINT "PK_AccountAuditEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE TABLE "Sellers" (
        "Id" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "ImportedName" character varying(120) NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_Sellers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE TABLE "ClosingSnapshots" (
        "Id" uuid NOT NULL,
        "SellerId" uuid NOT NULL,
        "Year" integer NOT NULL,
        "Month" integer NOT NULL,
        "Status" character varying(32) NOT NULL,
        "SnapshotJson" jsonb,
        "ReviewedBy" character varying(450) NOT NULL,
        "ReviewedAtUtc" timestamp with time zone NOT NULL,
        "ApprovedBy" character varying(450),
        "ApprovedAtUtc" timestamp with time zone,
        "Revision" uuid NOT NULL,
        CONSTRAINT "PK_ClosingSnapshots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ClosingSnapshots_Sellers_SellerId" FOREIGN KEY ("SellerId") REFERENCES "Sellers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE TABLE "UserSellerAccesses" (
        "UserId" text NOT NULL,
        "SellerId" uuid NOT NULL,
        "IsActive" boolean NOT NULL,
        "Permissions_CanViewRevenue" boolean NOT NULL,
        "Permissions_CanViewCommission" boolean NOT NULL,
        "Permissions_CanViewPrize" boolean NOT NULL,
        "Permissions_CanViewPPP" boolean NOT NULL,
        "Permissions_CanViewGoals" boolean NOT NULL,
        "Permissions_CanViewTrades" boolean NOT NULL,
        "Permissions_CanViewCustomers" boolean NOT NULL,
        CONSTRAINT "PK_UserSellerAccesses" PRIMARY KEY ("UserId", "SellerId"),
        CONSTRAINT "FK_UserSellerAccesses_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_UserSellerAccesses_Sellers_SellerId" FOREIGN KEY ("SellerId") REFERENCES "Sellers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE INDEX "IX_AccountAuditEvents_OccurredAtUtc" ON "AccountAuditEvents" ("OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE UNIQUE INDEX "IX_ClosingSnapshots_SellerId_Year_Month" ON "ClosingSnapshots" ("SellerId", "Year", "Month");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE UNIQUE INDEX "IX_Sellers_ImportedName" ON "Sellers" ("ImportedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    CREATE INDEX "IX_UserSellerAccesses_SellerId" ON "UserSellerAccesses" ("SellerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905171228_AddSellerPortal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260905171228_AddSellerPortal', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905174543_AddSellerSelfRegistration') THEN
    ALTER TABLE "AspNetUsers" ADD "IsRegistrationPending" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905174543_AddSellerSelfRegistration') THEN
    ALTER TABLE "AspNetUsers" ADD "RegistrationName" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905174543_AddSellerSelfRegistration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260905174543_AddSellerSelfRegistration', '10.0.11');
    END IF;
END $EF$;
COMMIT;
