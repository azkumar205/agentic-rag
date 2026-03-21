// Azure Document Intelligence — S0 tier

@description('Resource name')
param name string

@description('Azure region')
param location string

resource docIntel 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'FormRecognizer'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
  }
}

output endpoint string = docIntel.properties.endpoint
output name string = docIntel.name
output id string = docIntel.id
