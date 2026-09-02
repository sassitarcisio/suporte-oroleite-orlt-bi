# OroBI Dedicated Static Web App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the React SPA to a dedicated `orobi-web` Static Web App and permit it to call the production API without changing `orlt-bi`.

**Architecture:** Azure creates `orobi-web` without source-control integration. The repository workflow is the sole publisher, using its deployment token stored in the GitHub `production` environment. Once Azure returns the hostname, the API deployment applies that exact origin through explicit safe arguments.

**Tech Stack:** Azure Static Web Apps, Azure Container Apps, GitHub Actions, React 19, Vite 8, Pester 3.

**Spec:** `docs/superpowers/specs/2026-09-02-orobi-web-static-app-design.md`

## Global Constraints

- Do not modify, redeploy, or delete the legacy `orlt-bi` Static Web App.
- Never print or commit the Static Web App deployment token.
- Run Web tests before a production upload.
- Pass the exact HTTPS hostname assigned to `orobi-web` as `-WebOrigin` when applying API CORS.

---

### Task 1: Provision `orobi-web`

**Files:**
- Modify: `docs/operations/azure-production.md`
- Modify: `docs/TODO.md`

**Interfaces:**
- Consumes: Azure subscription `Empresas`, resource group `rg-oroleite-site`.
- Produces: `orobi-web`, its `defaultHostname`, and its deployment token for Tasks 2-4.

- [ ] **Step 1: Verify the resource is absent before creation**

Run:

```powershell
az staticwebapp show --name orobi-web --resource-group rg-oroleite-site --output json
```

Expected: `ResourceNotFound`. If Azure returns an existing resource, record its hostname and do not create a second app.

- [ ] **Step 2: Create the dedicated app without GitHub integration**

Run:

```powershell
az staticwebapp create --name orobi-web --resource-group rg-oroleite-site --location eastus2 --sku Free --output json
```

Expected: Azure creates `orobi-web`; `orlt-bi` is not changed.

- [ ] **Step 3: Capture the hostname and token without outputting the token**

Run:

```powershell
$webHostname = az staticwebapp show --name orobi-web --resource-group rg-oroleite-site --query defaultHostname --output tsv
$deploymentToken = az staticwebapp secrets list --name orobi-web --resource-group rg-oroleite-site --query properties.apiKey --output tsv
if ([string]::IsNullOrWhiteSpace($webHostname) -or [string]::IsNullOrWhiteSpace($deploymentToken)) { throw 'orobi-web hostname or deployment token is unavailable.' }
```

Expected: `$webHostname` ends in `.azurestaticapps.net`; `$deploymentToken` stays only in the process.

- [ ] **Step 4: Record the dedicated resource**

Update `docs/operations/azure-production.md` and `docs/TODO.md` to identify `orobi-web` as the React SPA resource and `orlt-bi` as legacy. Record the hostname but not the token.

- [ ] **Step 5: Commit the provisioning documentation**

Run:

```powershell
git add docs/operations/azure-production.md docs/TODO.md
git commit -m "docs: record dedicated OroBI static app"
```

### Task 2: Gate Web Publication

**Files:**
- Modify: `.github/workflows/deploy-web.yml`
- Modify: `tests/Operations/GitHubWorkflow.Tests.ps1`

**Interfaces:**
- Consumes: `AZURE_STATIC_WEB_APPS_API_TOKEN` from GitHub environment `production` and `VITE_API_BASE_URL`.
- Produces: a test-gated workflow that uploads `src/OroBI.Web/dist`.

- [ ] **Step 1: Write the failing workflow contract test**

Add an `It` block that reads `deploy-web.yml` and asserts:

```powershell
$workflow | Should Match 'npm ci'
$workflow | Should Match 'npm test -- --run'
$workflow | Should Match 'AZURE_STATIC_WEB_APPS_API_TOKEN'
$workflow | Should Match 'VITE_API_BASE_URL: https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io'
```

- [ ] **Step 2: Verify the contract test fails**

Run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
Invoke-Pester tests/Operations/GitHubWorkflow.Tests.ps1
```

Expected: the new case fails because the workflow has no dependency install or Web test step.

- [ ] **Step 3: Add deterministic install and test steps before upload**

Insert after checkout in `.github/workflows/deploy-web.yml`:

```yaml
      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm
          cache-dependency-path: src/OroBI.Web/package-lock.json
      - name: Install Web dependencies
        working-directory: src/OroBI.Web
        run: npm ci
      - name: Test Web
        working-directory: src/OroBI.Web
        run: npm test -- --run
```

Keep the existing Static Web Apps upload action, app location, output location, build command, API base URL, and token secret.

- [ ] **Step 4: Verify the contract test passes**

Run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
Invoke-Pester tests/Operations/GitHubWorkflow.Tests.ps1
```

Expected: every workflow contract case passes.

- [ ] **Step 5: Run the local Web gate with the production API URL**

Run from `src/OroBI.Web`:

```powershell
$env:VITE_API_BASE_URL = 'https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io'
npm.cmd ci
npm.cmd test -- --run
npm.cmd run build
```

Expected: Web tests and the production build pass.

- [ ] **Step 6: Commit the workflow gate**

Run:

```powershell
git add .github/workflows/deploy-web.yml tests/Operations/GitHubWorkflow.Tests.ps1
git commit -m "ci: test OroBI web before static app deploy"
```

### Task 3: Configure GitHub and Publish the SPA

**Files:**
- Modify: `docs/operations/azure-production.md`
- Modify: `docs/TODO.md`

**Interfaces:**
- Consumes: `$deploymentToken` and `$webHostname` from Task 1; repository `sassitarcisio/suporte-oroleite-orlt-bi`.
- Produces: GitHub `production` secret and a published `orobi-web` SPA.

- [ ] **Step 1: Verify GitHub CLI access**

Run:

```powershell
gh auth status
gh repo view sassitarcisio/suporte-oroleite-orlt-bi --json nameWithOwner
```

Expected: the account can administer the repository's production environment secret.

- [ ] **Step 2: Store the token in GitHub without echoing it**

Run in the process that holds `$deploymentToken`:

```powershell
$deploymentToken | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --env production --repo sassitarcisio/suporte-oroleite-orlt-bi
```

Expected: GitHub accepts the secret and no token value is displayed.

- [ ] **Step 3: Trigger and inspect the workflow**

Run:

```powershell
gh workflow run 'Deploy OroBI Web' --repo sassitarcisio/suporte-oroleite-orlt-bi --ref main
gh run list --repo sassitarcisio/suporte-oroleite-orlt-bi --workflow 'Deploy OroBI Web' --limit 1
```

Expected: the newest run completes successfully.

- [ ] **Step 4: Verify the React root document**

Run:

```powershell
$webOrigin = "https://$webHostname"
$response = Invoke-WebRequest -UseBasicParsing "$webOrigin/"
if ($response.StatusCode -ne 200 -or $response.Content -notmatch '<div id="root"></div>') { throw 'orobi-web did not return the React SPA root document.' }
```

Expected: HTTP 200 and a React root element; `orlt-bi` remains unchanged.

- [ ] **Step 5: Record the publication and commit documentation**

Update `docs/operations/azure-production.md` and `docs/TODO.md` with the hostname and workflow result. Then run:

```powershell
git add docs/operations/azure-production.md docs/TODO.md
git commit -m "docs: record OroBI web publication"
```

### Task 4: Apply CORS and Validate Integration

**Files:**
- Modify: `docs/TODO.md`

**Interfaces:**
- Consumes: `$webOrigin` from Task 3 and the current API image.
- Produces: API CORS allowlist for `orobi-web` and verified SPA-to-API connectivity.

- [ ] **Step 1: Read the current API image**

Run:

```powershell
$apiImage = az containerapp show --name orobi-api --resource-group rg-oroleite-site --query properties.template.containers[0].image --output tsv
if ([string]::IsNullOrWhiteSpace($apiImage)) { throw 'Could not resolve the current API image.' }
```

Expected: the image URI currently serving the API.

- [ ] **Step 2: Review the explicit CORS what-if**

Run:

```powershell
.\scripts\deploy-azure.ps1 -ConfigureRuntimeSecrets -ApiImage $apiImage -WebOrigin $webOrigin
```

Expected: no predicted removal of the database secret, selected image, or CORS origin.

- [ ] **Step 3: Apply the reviewed CORS change**

Run:

```powershell
.\scripts\deploy-azure.ps1 -Apply -ConfigureRuntimeSecrets -ApiImage $apiImage -WebOrigin $webOrigin
```

Expected: Container App provisioning succeeds.

- [ ] **Step 4: Verify health and CORS preflight**

Run:

```powershell
Invoke-WebRequest -UseBasicParsing 'https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io/health'
curl.exe -i -X OPTIONS 'https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io/api/auth/login' -H "Origin: $webOrigin" -H 'Access-Control-Request-Method: POST'
```

Expected: health is HTTP 200 and preflight includes `Access-Control-Allow-Origin: $webOrigin`.

- [ ] **Step 5: Validate login with an authorized account and commit the record**

Open `$webOrigin`, sign in with an authorized OroBI account, and confirm no browser CORS error occurs. Do not record credentials. Update `docs/TODO.md` with health, preflight, and login results, then run:

```powershell
git add docs/TODO.md
git commit -m "docs: record OroBI web integration validation"
```

## Plan Self-Review

- Spec coverage: Tasks 1-4 cover dedicated provisioning, secret handling, workflow test/build/upload, API CORS, health, preflight, login, and preservation of `orlt-bi`.
- Placeholder scan: the hostname is obtained from Azure before use and no unresolved implementation markers remain.
- Interface consistency: Task 1 emits hostname/token; Task 3 publishes with them; Task 4 uses the exact hostname and current image.
