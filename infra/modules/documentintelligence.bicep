// Azure AI Document Intelligence for multimodal extraction (OCR, tables, images)
@description('Document Intelligence account name')
param docIntelligenceName string

@description('Azure region')
param location string

resource docIntelligence 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: docIntelligenceName
  location: location
  kind: 'FormRecognizer'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: docIntelligenceName
    publicNetworkAccess: 'Enabled'
  }
}

output endpoint string = docIntelligence.properties.endpoint
output apiKey string = docIntelligence.listKeys().key1
