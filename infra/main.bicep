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

@description('Principal ID (object ID) of the identity that will run the application (e.g., your user, managed identity, or service principal). Used to assign RBAC roles for Search and OpenAI access.')
param appPrincipalId string = ''

@description('Principal type for the appPrincipalId. Use "User" for interactive developer logins (az ad signed-in-user), "ServicePrincipal" for managed identities and service principals.')
@allowed(['User', 'ServicePrincipal', 'Group'])
param appPrincipalType string = 'User'

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

// ─── RBAC Role Assignments (optional — requires appPrincipalId to be set) ───
// These roles are required for the full ingestion pipeline:
//   - Search Index Data Contributor: create/update the rag-index and upload documents
//   - Search Service Contributor:    manage index schema (CreateOrUpdateIndex)
//   - Cognitive Services OpenAI User: call Azure OpenAI embedding and chat APIs
//
// For developer login:       appPrincipalId=$(az ad signed-in-user show --query id -o tsv)  appPrincipalType=User
// For managed identity:      appPrincipalId=<managed-identity-object-id>                   appPrincipalType=ServicePrincipal

resource searchIndexDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(appPrincipalId)) {
  name: guid(aiSearch.outputs.id, appPrincipalId, '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7') // Search Index Data Contributor
    principalId: appPrincipalId
    principalType: appPrincipalType
  }
}

resource searchIndexDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(appPrincipalId)) {
  name: guid(aiSearch.outputs.id, appPrincipalId, '1407120a-92aa-4202-b7e9-c0e197c71c8f')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '1407120a-92aa-4202-b7e9-c0e197c71c8f') // Search Index Data Reader
    principalId: appPrincipalId
    principalType: appPrincipalType
  }
}

resource openAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(appPrincipalId)) {
  name: guid(openai.outputs.id, appPrincipalId, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd') // Cognitive Services OpenAI User
    principalId: appPrincipalId
    principalType: appPrincipalType
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
