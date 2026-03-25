# ═══════════════════════════════════════════════════════════════
# Deploy Agentic RAG Infrastructure
# ═══════════════════════════════════════════════════════════════
# Usage:
#   .\deploy.ps1 -Suffix "agentic01" -Location "centralindia" -SqlPassword "YourStr0ngP@ss1!"
# ═══════════════════════════════════════════════════════════════

param(
    [string]$Suffix = "agentic01",
    [string]$Location = "centralindia",
    [string]$OpenAILocation = "eastus2",
    [string]$ResourceGroup = "rg-agentic-rag",
    [string]$SqlUser = "sqladmin",
    [Parameter(Mandatory=$true)]
    [string]$SqlPassword,
    [string]$AppSku = "B1"
)

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Deploying Agentic RAG Infrastructure" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan

# 1. Create Resource Group
Write-Host "`n[1/5] Creating resource group '$ResourceGroup'..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none

# 2. Deploy Bicep
Write-Host "`n[2/5] Deploying Bicep template (this takes 5-10 minutes)..." -ForegroundColor Yellow
$result = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$PSScriptRoot\main.bicep" `
    --parameters suffix=$Suffix `
                 location=$Location `
                 openAILocation=$OpenAILocation `
                 sqlAdminUser=$SqlUser `
                 sqlAdminPassword=$SqlPassword `
                 appServiceSku=$AppSku `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment FAILED. Check errors above." -ForegroundColor Red
    exit 1
}

$outputs = $result.properties.outputs

# 3. Retrieve keys for setup configuration
Write-Host "`n[3/5] Retrieving storage and OpenAI keys..." -ForegroundColor Yellow
$storageName = $outputs.storageAccountName.value
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $storageName --query "[0].value" -o tsv
$storageConnStr = "DefaultEndpointsProtocol=https;AccountName=$storageName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"

$openAiName = "openai-$Suffix"
$openAiKey = az cognitiveservices account keys list --resource-group $ResourceGroup --name $openAiName --query "key1" -o tsv

Write-Host "  ✓ Keys retrieved." -ForegroundColor Green

# 4. Auto-generate Setup project appsettings.json
Write-Host "`n[4/5] Generating Setup project configuration..." -ForegroundColor Yellow
$setupConfig = @{
    AzureAISearch = @{
        Endpoint     = $outputs.searchEndpoint.value
        IndexName    = "agentic-rag-index"
        SemanticConfig = "agentic-rag-semantic"
    }
    AzureOpenAI = @{
        Endpoint            = $outputs.openAIEndpoint.value
        ApiKey              = $openAiKey
        EmbeddingDeployment = "text-embedding-3-large"
        EmbeddingDimensions = 1536
    }
    BlobStorage = @{
        ConnectionString = $storageConnStr
        ContainerName    = "documents"
    }
} | ConvertTo-Json -Depth 3

$setupConfigPath = Join-Path $PSScriptRoot "..\src\AgenticRAG.Setup\appsettings.json"
$setupConfig | Out-File -FilePath $setupConfigPath -Encoding utf8 -Force
Write-Host "  ✓ Written to src/AgenticRAG.Setup/appsettings.json" -ForegroundColor Green

# 5. Display outputs
Write-Host "`n[5/5] Deployment complete! Resource details:" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan

Write-Host "  Search Endpoint:     $($outputs.searchEndpoint.value)"
Write-Host "  OpenAI Endpoint:     $($outputs.openAIEndpoint.value)"
Write-Host "  Storage Account:     $storageName"
Write-Host "  SQL Server FQDN:    $($outputs.sqlServerFqdn.value)"
Write-Host "  Redis Hostname:      $($outputs.redisHostName.value)"
Write-Host "  App Service URL:     $($outputs.appServiceUrl.value)"
Write-Host "  App Insights:        (connection string in app settings)"
Write-Host "  Web App Principal:   $($outputs.webAppPrincipalId.value)"

Write-Host "`n═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Next Steps:" -ForegroundColor Yellow
Write-Host "    1. Run SQL setup:" -ForegroundColor White
Write-Host "       sqlcmd -S $($outputs.sqlServerFqdn.value) -d agenticragdb -U $SqlUser -P <password> -i ..\sql\setup.sql"
Write-Host "    2. Run search pipeline setup:" -ForegroundColor White
Write-Host "       cd ..\src\AgenticRAG.Setup; dotnet run"
Write-Host "    3. Upload PDFs to Blob container 'documents'" -ForegroundColor White
Write-Host "    4. Start API:" -ForegroundColor White
Write-Host "       cd ..\src\AgenticRAG.Api; dotnet run"
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
