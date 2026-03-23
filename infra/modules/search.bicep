// Azure AI Search for vector + hybrid RAG search
@description('Search service name')
param searchServiceName string

@description('Azure region')
param location string

@description('Index name for RAG documents')
param indexName string = 'rag-documents'

@description('SKU for the search service')
@allowed(['basic', 'standard', 'standard2', 'standard3'])
param sku string = 'standard'

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: sku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    semanticSearch: 'standard'
  }
}

output searchServiceName string = searchService.name
output endpoint string = 'https://${searchService.name}.search.windows.net'
output adminKey string = searchService.listAdminKeys().primaryKey
output indexName string = indexName
