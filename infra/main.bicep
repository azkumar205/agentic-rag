// Main Bicep entry point for Agentic RAG Azure infrastructure.
// Deploys all resources needed for Classic RAG (Phase 1) and Agentic RAG (Phase 2).

@description('Environment name: dev, staging, or prod')
param environmentName string = 'dev'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Unique suffix to ensure globally unique resource names')
param resourceSuffix string = uniqueString(resourceGroup().id)

@description('SQL Server administrator login')
param sqlAdminLogin string = 'sqladmin'

@secure()
@description('SQL Server administrator password')
param sqlAdminPassword string

// Modules
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    storageAccountName: 'st${environmentName}${resourceSuffix}'
    location: location
    containerName: 'documents'
  }
}

module search 'modules/search.bicep' = {
  name: 'search-deployment'
  params: {
    searchServiceName: 'srch-${environmentName}-${resourceSuffix}'
    location: location
    indexName: 'rag-documents'
  }
}

module openai 'modules/openai.bicep' = {
  name: 'openai-deployment'
  params: {
    openAiAccountName: 'oai-${environmentName}-${resourceSuffix}'
    location: location
  }
}

module documentIntelligence 'modules/documentintelligence.bicep' = {
  name: 'docintelligence-deployment'
  params: {
    docIntelligenceName: 'di-${environmentName}-${resourceSuffix}'
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql-deployment'
  params: {
    sqlServerName: 'sql-${environmentName}-${resourceSuffix}'
    sqlDatabaseName: 'ragdb'
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module appservice 'modules/appservice.bicep' = {
  name: 'appservice-deployment'
  params: {
    appServicePlanName: 'asp-${environmentName}-${resourceSuffix}'
    webAppName: 'app-agentic-rag-${environmentName}-${resourceSuffix}'
    location: location
    storageConnectionString: storage.outputs.connectionString
    searchEndpoint: search.outputs.endpoint
    searchApiKey: search.outputs.adminKey
    openAiEndpoint: openai.outputs.endpoint
    openAiApiKey: openai.outputs.apiKey
    docIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    docIntelligenceApiKey: documentIntelligence.outputs.apiKey
    sqlConnectionString: sql.outputs.connectionString
  }
}

// Outputs
output storageAccountName string = storage.outputs.storageAccountName
output searchServiceName string = search.outputs.searchServiceName
output openAiEndpoint string = openai.outputs.endpoint
output webAppUrl string = appservice.outputs.webAppUrl
