targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Globally unique prefix using only lowercase letters and numbers.')
@minLength(3)
@maxLength(18)
param prefix string

@secure()
@description('PostgreSQL administrator password. Supply from the deployment pipeline or Key Vault, never source control.')
param postgresAdministratorPassword string

@description('Container image for the API, for example myregistry.azurecr.io/orobi-api:sha.')
param apiImage string

@description('Enables the Key Vault database secret on the API after RBAC propagation.')
param configureRuntimeSecrets bool = false

@description('Enables initial administrator provisioning in the manual migration job.')
param configureInitialAdministrators bool = false

@description('Allowed browser origin for the published web application.')
param webOrigin string = ''

var storageName = '${prefix}store'
var postgresName = '${prefix}-postgres'
var environmentName = '${prefix}-cae'
var apiName = '${prefix}-api'
var apiIdentityName = '${prefix}-api-identity'
var migrationJobName = '${prefix}-migrate'
var logName = '${prefix}-logs'
var keyVaultName = '${prefix}kv'
var registryName = '${prefix}acr'
var acrPullRoleDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var keyVaultSecretsUserRoleDefinitionId = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributorRoleDefinitionId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-appi'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource importsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'imports'
  properties: { publicAccess: 'None' }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: apiIdentityName
  location: location
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresName
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    administratorLogin: 'orobiadmin'
    administratorLoginPassword: postgresAdministratorPassword
    version: '16'
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
  }
}

resource postgresAllowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: { customerId: logs.properties.customerId, sharedKey: logs.listKeys().primarySharedKey }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  dependsOn: [
    apiAcrPull
    apiKeyVaultSecretsUser
    apiStorageBlobDataContributor
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'auto' }
      registries: [
        {
          server: registry.properties.loginServer
          identity: apiIdentity.id
        }
      ]
      secrets: concat(configureRuntimeSecrets ? [
        {
          name: 'database-connection'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-database-connection'
          identity: apiIdentity.id
        }
      ] : [], [
        {
          name: 'jwt-signing-key'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-jwt-signing-key'
          identity: apiIdentity.id
        }
      ])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: concat(configureRuntimeSecrets ? [
            { name: 'ConnectionStrings__OroBi', secretRef: 'database-connection' }
          ] : [], [
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
            { name: 'ImportStorage__BlobServiceUri', value: 'https://${storage.name}.blob.${az.environment().suffixes.storage}' }
            { name: 'ImportStorage__ContainerName', value: importsContainer.name }
            { name: 'Jwt__Issuer', value: 'OroBI' }
            { name: 'Jwt__Audience', value: 'OroBI' }
            { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
          ], empty(webOrigin) ? [] : [
            { name: 'Cors__Origins__0', value: webOrigin }
          ])
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, apiIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleDefinitionId)
  }
}

resource apiKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, apiIdentity.id, keyVaultSecretsUserRoleDefinitionId)
  scope: vault
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleDefinitionId)
  }
}

resource apiStorageBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, apiIdentity.id, storageBlobDataContributorRoleDefinitionId)
  scope: storage
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleDefinitionId)
  }
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: migrationJobName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  dependsOn: [
    apiAcrPull
    apiKeyVaultSecretsUser
  ]
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: apiIdentity.id
        }
      ]
      secrets: concat([
        {
          name: 'database-connection'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-database-connection'
          identity: apiIdentity.id
        }
      ], configureInitialAdministrators ? [
        {
          name: 'initial-admin-0-password'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-initial-admin-0-password'
          identity: apiIdentity.id
        }
        {
          name: 'initial-admin-1-password'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-initial-admin-1-password'
          identity: apiIdentity.id
        }
      ] : [])
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: apiImage
          command: configureInitialAdministrators ? [ 'dotnet', 'OroBI.Api.dll', '--migrate', '--provision-admin' ] : [ 'dotnet', 'OroBI.Api.dll', '--migrate' ]
          env: concat([
            { name: 'ConnectionStrings__OroBi', secretRef: 'database-connection' }
          ], configureInitialAdministrators ? [
            { name: 'InitialAdmins__0__Email', value: 'tarcisio.sassi@oroleite.com.br' }
            { name: 'InitialAdmins__0__Password', secretRef: 'initial-admin-0-password' }
            { name: 'InitialAdmins__1__Email', value: 'jeferson@oroleite.com.br' }
            { name: 'InitialAdmins__1__Password', secretRef: 'initial-admin-1-password' }
          ] : [])
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
        }
      ]
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output storageAccountName string = storage.name
output keyVaultUri string = vault.properties.vaultUri
