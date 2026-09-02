# Azure Production

Deploy `infra/main.bicep` at resource-group scope with a unique lowercase `prefix` and the API image URI. Database secret values are stored in Key Vault and are not passed as deployment command arguments.

The template provisions low-cost consumption compute, PostgreSQL Flexible Server B1ms, private import blob storage, Key Vault, Application Insights, and Log Analytics. Before production, restrict PostgreSQL and Key Vault network access to the chosen virtual network and grant the API managed identity `Key Vault Secrets User` and `Storage Blob Data Contributor` through Azure RBAC.

## Deployment Prerequisites

Configure the GitHub `production` environment with these secrets. Do not place their values in repository files or workflow inputs.

| Secret | Purpose |
| --- | --- |
| `AZURE_CLIENT_ID` | Client ID of the Azure workload identity used by GitHub Actions. |
| `AZURE_TENANT_ID` | Tenant that contains the deployment subscription. |
| `AZURE_SUBSCRIPTION_ID` | Subscription that contains the target resource group. |
| `POSTGRES_ADMINISTRATOR_PASSWORD` | Stored as `orobi-postgres-administrator-password` by the bootstrap script. |
| `OROBI_DATABASE_CONNECTION_STRING` | Stored as `orobi-database-connection` and consumed by the API runtime. |

The Azure identity must have permission to deploy the resource group. Configure GitHub OIDC federated credentials for the `production` environment before triggering the workflow.

## Workflow Inputs

Run the `Deploy Azure` workflow manually with:

| Input | Requirement |
| --- | --- |
| `resource_group` | Existing Azure resource group in the target subscription. |
| `prefix` | Globally unique, lowercase letters and numbers only, from 3 to 18 characters. |
| `api_image` | Published API container image accessible to Azure Container Apps, such as `registry.example/orobi-api:<tag>`. |

The workflow validates `infra/main.bicep` and then runs a resource-group deployment. It does not build or publish the API image; publish that image before deployment.

## API Image Publication

The API image is built remotely in Azure Container Registry, so Docker is not required on the developer machine. Run these commands from the repository root after authenticating to Azure:

```powershell
az deployment group create --resource-group rg-oroleite-site --template-file infra/acr.bicep --parameters prefix=orobi
az acr build --registry orobiacr --image orobi-api:20260831 --file src/OroBI.Api/Dockerfile .
```

`20260831` is the initial traceable image tag. Use a new release tag for every later publication, then pass `orobiacr.azurecr.io/orobi-api:<tag>` to the deployment workflow as `api_image`.

## React Static Web App

The React SPA is published to the dedicated Static Web App `orobi-web` at `https://lively-sea-0776c9a0f.6.azurestaticapps.net`. The legacy `orlt-bi` Static Web App is a separate application and must not be redeployed by this repository.

Store the `orobi-web` deployment token as `AZURE_STATIC_WEB_APPS_API_TOKEN` in the GitHub `production` environment. The `Deploy OroBI Web` workflow publishes `src/OroBI.Web` and is the only supported deployment path for this SPA.

## Secure Pre-Deploy

The deploy operator needs `Key Vault Secrets Officer`. Set the two database values in the current secure process environment, then upload them to Key Vault. The API identity receives `Key Vault Secrets User` during the base deployment.

```powershell
.\scripts\bootstrap-azure-secrets.ps1
```

Run a `what-if` with the image and origin that will be published.

Set the image and Static Web App origin explicitly. This prevents an `-Apply` invocation from reverting a published image, removing CORS, or disabling the runtime database secret reference.

```powershell
$apiImage = 'orobiacr.azurecr.io/orobi-api:<release-tag>'
$webOrigin = 'https://<static-web-app>.azurestaticapps.net'
.\scripts\deploy-azure.ps1 -ApiImage $apiImage -WebOrigin $webOrigin
```

Run the actual deployment only after reviewing the `what-if` output:

```powershell
.\scripts\deploy-azure.ps1 -Apply -ApiImage $apiImage -WebOrigin $webOrigin -ConfigureRuntimeSecrets
```

The script requires `-ApiImage`, `-WebOrigin`, and `-ConfigureRuntimeSecrets` whenever `-Apply` is used. The runtime secret reference is already included in the command above.
