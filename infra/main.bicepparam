using 'main.bicep'

param baseName = 'agenticrag'
param location = 'eastus'
param sqlAdminUser = ''       // Set via --parameters at deploy time
param sqlAdminPassword = ''   // Set via --parameters at deploy time
param redisSku = 'Enterprise_E10'
param appPrincipalId = ''     // Set via --parameters at deploy time (az ad signed-in-user show --query id -o tsv)
param appPrincipalType = 'User'
