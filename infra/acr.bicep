targetScope = 'resourceGroup'

param location string = resourceGroup().location

@minLength(3)
@maxLength(18)
param prefix string

var registryName = '${prefix}acr'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output loginServer string = registry.properties.loginServer
