// Azure Container Registry — for Foundry hosted agent deployment

@description('Resource name (alphanumeric only)')
param name string

@description('Azure region')
param location string

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: name
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
  }
}

output loginServer string = acr.properties.loginServer
output name string = acr.name
output id string = acr.id
