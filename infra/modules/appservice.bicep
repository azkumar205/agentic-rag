// Azure App Service for hosting the AgenticRag.Api
@description('App Service Plan name')
param appServicePlanName string

@description('Web App name')
param webAppName string

@description('Azure region')
param location string

@description('Azure Storage connection string')
param storageConnectionString string

@description('Azure AI Search endpoint')
param searchEndpoint string

@description('Azure AI Search admin key')
@secure()
param searchApiKey string

@description('Azure OpenAI endpoint')
param openAiEndpoint string

@description('Azure OpenAI API key')
@secure()
param openAiApiKey string

@description('Document Intelligence endpoint')
param docIntelligenceEndpoint string

@description('Document Intelligence API key')
@secure()
param docIntelligenceApiKey string

@description('Azure SQL connection string')
@secure()
param sqlConnectionString string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      appSettings: [
        { name: 'AzureStorage__ConnectionString', value: storageConnectionString }
        { name: 'AzureSearch__Endpoint', value: searchEndpoint }
        { name: 'AzureSearch__ApiKey', value: searchApiKey }
        { name: 'AzureSearch__IndexName', value: 'rag-documents' }
        { name: 'AzureOpenAI__Endpoint', value: openAiEndpoint }
        { name: 'AzureOpenAI__ApiKey', value: openAiApiKey }
        { name: 'AzureOpenAI__EmbeddingDeployment', value: 'text-embedding-ada-002' }
        { name: 'AzureOpenAI__ChatDeployment', value: 'gpt-4o' }
        { name: 'DocumentIntelligence__Endpoint', value: docIntelligenceEndpoint }
        { name: 'DocumentIntelligence__ApiKey', value: docIntelligenceApiKey }
        { name: 'SqlDatabase__ConnectionString', value: sqlConnectionString }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      ]
    }
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
