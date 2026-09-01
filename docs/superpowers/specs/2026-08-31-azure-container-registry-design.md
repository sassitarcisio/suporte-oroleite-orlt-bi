# Azure Container Registry Design

## Objective

Publish the OroBI API image without requiring Docker on the developer machine and allow the Azure Container App to pull that image without registry passwords.

## Scope

- Add a Basic Azure Container Registry through a small bootstrap Bicep deployment, then reference it from the existing resource-group deployment.
- Derive its globally unique name from the existing lowercase `prefix` as `${prefix}acr`.
- Disable the ACR administrator user.
- Assign the built-in `AcrPull` role to the system-assigned identity of the API Container App at the registry scope.
- Build and tag the API image through `az acr build` from the repository root using `src/OroBI.Api/Dockerfile`.
- Keep `apiImage` as an explicit Bicep parameter so CI/CD controls the deployed tag.

## Deployment Flow

1. Provision or update the ACR through `infra/acr.bicep`.
2. Build `orobi-api:<tag>` remotely with `az acr build`, using the repository root as the build context.
3. Deploy `infra/main.bicep`, which references the existing registry, with the resulting `<registry>.azurecr.io/orobi-api:<tag>` value for `apiImage`.
4. Azure Container Apps uses its managed identity and the scoped `AcrPull` assignment to retrieve the image.

## Security

- No registry administrator credentials, image pull secrets, passwords, or connection strings are committed.
- The API identity receives only `AcrPull`, scoped to its registry.
- The deployment identity must have sufficient permission to create the registry and role assignment in the resource group.

## Validation

- `az bicep build --file infra/main.bicep` succeeds.
- `az acr build` reports a successful image build.
- `az deployment group what-if` reports the intended ACR, Container App, and role-assignment changes before deployment.
- After deployment, the API Container App reaches `/health` and reports a running revision with the requested image tag.

## Constraints

- The target resource group is `rg-oroleite-site` in `eastus2`.
- A live deployment still requires the PostgreSQL and API connection-string secrets defined in the production guide.
- Existing Static Web Apps and storage resources are not modified by this change.
