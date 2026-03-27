// ═══════════════════════════════════════════════════════════════
// Agentic RAG — Complete Azure Infrastructure (Bicep)
// Deploy: az deployment group create -g rg-agentic-rag -f main.bicep
// ═══════════════════════════════════════════════════════════════

@description('Unique suffix for all resource names (lowercase, no hyphens)')
param suffix string = 'agentic01'

@description('Primary location for most resources')
param location string = resourceGroup().location

@description('Location for Azure OpenAI (may differ due to model availability)')
param openAILocation string = 'eastus2'

@description('SQL admin username')
param sqlAdminUser string = 'sqladmin'

@secure()
@description('SQL admin password (min 8 chars, uppercase, lowercase, number, special)')
param sqlAdminPassword string

@description('App Service SKU')
param appServiceSku string = 'B1'

// ── Variables ────────────────────────────────────────
var searchName = 'search-${suffix}'
var openAIName = 'openai-${suffix}'
var storageName = 'strag${suffix}'
var docIntelName = 'docintel-${suffix}'
var sqlServerName = 'sql-${suffix}'
var sqlDbName = 'agenticragdb'
var redisName = 'redis-${suffix}'
var kvName = 'kv-${suffix}'
var planName = 'plan-${suffix}'
var appName = 'app-${suffix}'
var swaName = 'chat-${suffix}'
var insightsName = 'appinsights-${suffix}'
var logWorkspaceName = 'log-${suffix}'

// ═══════════════════════════════════════════════════════════════
// 1. Log Analytics Workspace (required by App Insights)
// ═══════════════════════════════════════════════════════════════
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logWorkspaceName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ═══════════════════════════════════════════════════════════════
// 2. Azure AI Search (Basic — supports indexers + skillsets)
// ═══════════════════════════════════════════════════════════════
resource search 'Microsoft.Search/searchServices@2024-03-01-preview' = {
  name: searchName
  location: location
  sku: { name: 'basic' }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
}

// ═══════════════════════════════════════════════════════════════
// 3. Azure OpenAI
// ═══════════════════════════════════════════════════════════════
resource openAI 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: 'text-embedding-3-large'
  sku: {
    name: 'Standard'
    capacity: 120
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
  }
  dependsOn: [gpt4oDeployment]   // Serial deployment to avoid conflicts
}

// ── Cost Optimization: GPT-4o-mini (planning, reflection, summarization) ──
resource gpt4oMiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: 'gpt-4o-mini'
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
  }
  dependsOn: [embeddingDeployment]
}

// ── Cost Optimization: text-embedding-3-small (semantic cache only) ──
resource cacheEmbeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: 'text-embedding-3-small'
  sku: {
    name: 'Standard'
    capacity: 120
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-small'
      version: '1'
    }
  }
  dependsOn: [gpt4oMiniDeployment]
}

// ═══════════════════════════════════════════════════════════════
// 4. Storage Account + Containers
// ═══════════════════════════════════════════════════════════════
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'documents'
}

resource imagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'images'
}

// ═══════════════════════════════════════════════════════════════
// 5. Document Intelligence (Free tier)
// ═══════════════════════════════════════════════════════════════
resource docIntel 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: docIntelName
  location: location
  kind: 'FormRecognizer'
  sku: { name: 'F0' }
  properties: {
    customSubDomainName: docIntelName
    publicNetworkAccess: 'Enabled'
  }
}

// ═══════════════════════════════════════════════════════════════
// 6. Azure SQL Server + Database
// ═══════════════════════════════════════════════════════════════
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUser
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
}

// Allow Azure services to access SQL
resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ═══════════════════════════════════════════════════════════════
// 7. Azure Cache for Redis
// ═══════════════════════════════════════════════════════════════
resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: redisName
  location: location
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

// ═══════════════════════════════════════════════════════════════
// 8. Key Vault
// ═══════════════════════════════════════════════════════════════
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// ═══════════════════════════════════════════════════════════════
// 9. App Service Plan + Web App
// ═══════════════════════════════════════════════════════════════
resource appPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureAISearch__Endpoint', value: 'https://${search.name}.search.windows.net' }
        { name: 'AzureOpenAI__Endpoint', value: openAI.properties.endpoint }
        { name: 'AzureOpenAI__ChatDeployment', value: 'gpt-4o' }
        { name: 'AzureOpenAI__PlanningDeployment', value: 'gpt-4o-mini' }
        { name: 'AzureOpenAI__EmbeddingDeployment', value: 'text-embedding-3-large' }
        { name: 'AzureOpenAI__EmbeddingDimensions', value: '1536' }
        { name: 'AzureOpenAI__CacheEmbeddingDeployment', value: 'text-embedding-3-small' }
        { name: 'AzureOpenAI__CacheEmbeddingDimensions', value: '512' }
        { name: 'SqlServer__ConnectionString', value: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDbName};User Id=${sqlAdminUser};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;' }
        { name: 'BlobStorage__ConnectionString', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value}' }
        { name: 'Redis__ConnectionString', value: '${redis.properties.hostName}:${redis.properties.sslPort},password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'Cors__AllowedOrigins__0', value: 'https://${staticWebApp.properties.defaultHostname}' }
        { name: 'Cors__AllowedOrigins__1', value: 'http://localhost:3000' }
      ]
      cors: {
        allowedOrigins: [
          'https://${staticWebApp.properties.defaultHostname}'
          'http://localhost:3000'
        ]
      }
    }
  }
}

// ═══════════════════════════════════════════════════════════════
// 10. Azure Static Web App (React Frontend)
// ═══════════════════════════════════════════════════════════════
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ═══════════════════════════════════════════════════════════════
// 11. Application Insights
// ═══════════════════════════════════════════════════════════════
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: insightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
}

// ═══════════════════════════════════════════════════════════════
// RBAC: Give Web App access to Search, OpenAI, Storage, Key Vault
// ═══════════════════════════════════════════════════════════════
// Search Index Data Contributor
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, search.id, '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
  scope: search
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalType: 'ServicePrincipal'
  }
}

// Cognitive Services OpenAI User
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, openAI.id, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: openAI
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Data Contributor
resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, storage.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  scope: storage
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, kv.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: kv
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalType: 'ServicePrincipal'
  }
}

// ═══════════════════════════════════════════════════════════════
// Outputs — use these to configure your .NET app
// ═══════════════════════════════════════════════════════════════
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output openAIEndpoint string = openAI.properties.endpoint
output storageAccountName string = storage.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output redisHostName string = redis.properties.hostName
output appServiceUrl string = 'https://${webApp.properties.defaultHostName}'
output appInsightsConnectionString string = insights.properties.ConnectionString
output webAppPrincipalId string = webApp.identity.principalId
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppName string = staticWebApp.name
