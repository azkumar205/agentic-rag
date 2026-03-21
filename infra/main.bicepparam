using 'main.bicep'

param baseName = 'agenticrag'
param location = 'eastus'
param sqlAdminUser = ''       // Set via --parameters at deploy time
param sqlAdminPassword = ''   // Set via --parameters at deploy time
param redisSku = 'Enterprise_E10'
