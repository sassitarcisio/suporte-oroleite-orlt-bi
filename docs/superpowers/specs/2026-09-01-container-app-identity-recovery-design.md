# Container App Identity Recovery Design

## Objective

Recover the failed `orobi-api` Container App revision without deleting existing
Azure resources. Remove the identity bootstrap cycle that blocks ACR image
pulls during Container App creation.

## Design

1. Create a user-assigned managed identity named `${prefix}-api-identity`.
2. Assign `AcrPull` to that identity at the existing `${prefix}acr` registry.
3. Assign `Key Vault Secrets User` to that identity at the existing `${prefix}kv`
   vault.
4. Configure the Container App with that user-assigned identity and use its
   resource ID for the ACR registry and Key Vault secret reference.
5. Preserve all existing resources, including PostgreSQL, storage, Log
   Analytics, Application Insights, managed environment, ACR and Key Vault.

## Deployment Sequence

The Bicep template creates the user-assigned identity and both role assignments
before the Container App. The Container App depends on those assignments. Its
runtime Key Vault reference remains gated by `configureRuntimeSecrets`, so the
base recovery deploy creates a revision that can pull the image, then the
runtime deploy activates the database secret after RBAC propagation.

## Validation

- Pester asserts the identity resource, role assignments and explicit
  Container App dependencies.
- Bicep compiles successfully.
- Azure deployment reports a ready Container App revision after the base stage.
- The final stage reports a ready revision with the Key Vault reference.
