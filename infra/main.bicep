// ──────────────────────────────────────────────────────
// Agentic RAG — Azure Infrastructure (Bicep)
// Provisions all Azure resources for the solution
// ──────────────────────────────────────────────────────

targetScope = 'resourceGroup'

// ─── Parameters ────────────────────────────────────
@description('Base name prefix for all resources')
@minLength(3)
@maxLength(15)
param baseName string

@description('Azure region for deployment')
param location string = resourceGroup().location

@description('SQL Server admin username')
@secure()
param sqlAdminUser string

@description('SQL Server admin password')
@secure()
param sqlAdminPassword string

@description('Redis Enterprise SKU')
@allowed(['Enterprise_E10', 'Enterprise_E20', 'Enterprise_E50'])
param redisSku string = 'Enterprise_E10'

@description('Azure OpenAI GPT-4o model version')
param gpt4oVersion string = '2024-08-06'

@description('Azure OpenAI embedding model version')
param embeddingVersion string = '2024-02-01'

// ─── Variables ─────────────────────────────────────
var uniqueSuffix = uniqueString(resourceGroup().id, baseName)
var names = {
  openai: 'oai-${baseName}-${uniqueSuffix}'
  search: 'srch-${baseName}-${uniqueSuffix}'
  docIntel: 'di-${baseName}-${uniqueSuffix}'
  redis: 'redis-${baseName}-${uniqueSuffix}'
  sql: 'sql-${baseName}-${uniqueSuffix}'
  sqlDb: 'db-${baseName}'
  acr: 'acr${baseName}${uniqueSuffix}'
  logAnalytics: 'log-${baseName}-${uniqueSuffix}'
  appInsights: 'ai-${baseName}-${uniqueSuffix}'
}

// ─── Modules ───────────────────────────────────────

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: names.logAnalytics
    location: location
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  params: {
    name: names.appInsights
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

module openai 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    name: names.openai
    location: location
    gpt4oVersion: gpt4oVersion
    embeddingVersion: embeddingVersion
  }
}

module aiSearch 'modules/ai-search.bicep' = {
  name: 'aiSearch'
  params: {
    name: names.search
    location: location
  }
}

module docIntelligence 'modules/doc-intelligence.bicep' = {
  name: 'docIntelligence'
  params: {
    name: names.docIntel
    location: location
  }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  params: {
    name: names.redis
    location: location
    skuName: redisSku
  }
}

module sql 'modules/sql-server.bicep' = {
  name: 'sql'
  params: {
    serverName: names.sql
    databaseName: names.sqlDb
    location: location
    adminUser: sqlAdminUser
    adminPassword: sqlAdminPassword
  }
}

module acr 'modules/container-registry.bicep' = {
  name: 'acr'
  params: {
    name: names.acr
    location: location
  }
}

// ─── Outputs ───────────────────────────────────────
output openaiEndpoint string = openai.outputs.endpoint
output openaiName string = openai.outputs.name
output searchEndpoint string = aiSearch.outputs.endpoint
output searchName string = aiSearch.outputs.name
output docIntelEndpoint string = docIntelligence.outputs.endpoint
output redisHostName string = redis.outputs.hostName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output acrLoginServer string = acr.outputs.loginServer
output appInsightsConnectionString string = appInsights.outputs.connectionString
