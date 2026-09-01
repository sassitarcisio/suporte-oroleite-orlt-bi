# PostgreSQL Migration Job Design

## Objective

Create the `orobi` database and apply EF Core migrations in Azure before the
API depends on its schema.

## Design

A Container Apps Job uses the API image and the existing user-assigned identity.
It reads the database connection string from `orobikv`, invokes a dedicated
`--migrate` application mode, connects to the administrative `postgres`
database to create `orobi` when absent, and then applies pending migrations.
The job has one replica and no retry loop beyond the Azure job retry policy.

## Sequence

1. Deploy the job after the user-assigned identity and Key Vault access exist.
2. Start the job explicitly after the runtime secret stage.
3. Confirm a successful execution before enabling business validation.

## Safety

The normal API mode never applies migrations. The job is the sole migration
actor, preventing concurrent schema changes from scaled API replicas.

## Validation

- Unit or integration tests cover the `--migrate` execution path.
- Bicep contract tests verify the job identity and Key Vault secret reference.
- Azure job execution must complete successfully before API validation.
