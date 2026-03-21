// Azure Cache for Redis — Enterprise with RediSearch module

@description('Resource name')
param name string

@description('Azure region')
param location string

@description('Enterprise SKU')
param skuName string

resource redisEnterprise 'Microsoft.Cache/redisEnterprise@2024-02-01' = {
  name: name
  location: location
  sku: {
    name: skuName
    capacity: 2
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

resource database 'Microsoft.Cache/redisEnterprise/databases@2024-02-01' = {
  parent: redisEnterprise
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    evictionPolicy: 'NoEviction'
    clusteringPolicy: 'EnterpriseCluster'
    modules: [
      { name: 'RediSearch' }
      { name: 'RedisJSON' }
    ]
  }
}

output hostName string = redisEnterprise.properties.hostName
output name string = redisEnterprise.name
output id string = redisEnterprise.id
