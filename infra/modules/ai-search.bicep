// Azure AI Search — Standard tier with semantic ranking

@description('Resource name')
param name string

@description('Azure region')
param location string

resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  sku: { name: 'standard' }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    semanticSearch: 'standard'
    publicNetworkAccess: 'enabled'
  }
}

output endpoint string = 'https://${search.name}.search.windows.net'
output name string = search.name
output id string = search.id
