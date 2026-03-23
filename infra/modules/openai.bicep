// Azure OpenAI for embeddings and GPT-4 chat
@description('Azure OpenAI account name')
param openAiAccountName string

@description('Azure region (must support Azure OpenAI)')
param location string

@description('GPT-4o chat deployment name')
param chatDeploymentName string = 'gpt-4o'

@description('Embedding model deployment name')
param embeddingDeploymentName string = 'text-embedding-ada-002'

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAiAccountName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAiAccountName
    publicNetworkAccess: 'Enabled'
  }
}

resource chatDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAiAccount
  name: chatDeploymentName
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-05-13'
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAiAccount
  name: embeddingDeploymentName
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
  }
  dependsOn: [chatDeployment]
}

output endpoint string = openAiAccount.properties.endpoint
output apiKey string = openAiAccount.listKeys().key1
output chatDeploymentName string = chatDeploymentName
output embeddingDeploymentName string = embeddingDeploymentName
