<#
.SYNOPSIS
    Deploys The Library app (frontend to SWA, API to Function App).

.PARAMETER SkipApi
    Skip the API deployment (deploy frontend only).

.PARAMETER SkipFrontend
    Skip the frontend deployment (deploy API only).

.PARAMETER SeedData
    Also seed/refresh users and book metadata in production storage.
#>

param(
    [switch]$SkipApi,
    [switch]$SkipFrontend,
    [switch]$SeedData
)

$ErrorActionPreference = "Stop"

# --- Configuration ---
$ResourceGroup   = "rg-the-library"
$FunctionAppName = "func-the-library"
$SwaName         = "swa-the-library"
$StorageAccount  = "stgthelibrary2026"
$ApiDir          = "api"

Write-Host "`n=== The Library - Deploy ===" -ForegroundColor Cyan

# Verify Azure CLI login
$account = az account show -o json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
}
Write-Host "Subscription: $($account.name)" -ForegroundColor Gray

# --- Deploy API ---
if (-not $SkipApi) {
    Write-Host "`n--- Deploying API to $FunctionAppName ---" -ForegroundColor Green

    Push-Location $ApiDir
    try {
        func azure functionapp publish $FunctionAppName
        if ($LASTEXITCODE -ne 0) { throw "API deployment failed." }
        Write-Host "API deployed successfully." -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# --- Deploy Frontend ---
if (-not $SkipFrontend) {
    Write-Host "`n--- Building frontend ---" -ForegroundColor Green
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }

    Write-Host "--- Deploying frontend to $SwaName ---" -ForegroundColor Green
    $deployToken = az staticwebapp secrets list `
        --name $SwaName `
        --resource-group $ResourceGroup `
        --query "properties.apiKey" -o tsv

    npx swa deploy dist --deployment-token $deployToken --env production
    if ($LASTEXITCODE -ne 0) { throw "Frontend deployment failed." }
    Write-Host "Frontend deployed successfully." -ForegroundColor Green
}

# --- Seed Data (optional) ---
if ($SeedData) {
    Write-Host "`n--- Seeding production data ---" -ForegroundColor Green

    if (-not $env:SEED_ADMIN_PASSWORD -or -not $env:SEED_READER_PASSWORD) {
        throw "SEED_ADMIN_PASSWORD and SEED_READER_PASSWORD must be set before seeding production data."
    }

    $connStr = az storage account show-connection-string `
        --name $StorageAccount `
        --resource-group $ResourceGroup `
        --query connectionString -o tsv

    $env:STORAGE_CONNECTION_STRING = $connStr
    node tools/seed-data.mjs
    if ($LASTEXITCODE -ne 0) { throw "Data seeding failed." }
    $env:STORAGE_CONNECTION_STRING = $null
    Write-Host "Production data seeded." -ForegroundColor Green
}

# --- Summary ---
Write-Host "`n=== Deployment complete ===" -ForegroundColor Cyan
if (-not $SkipFrontend) {
    Write-Host "Frontend: https://icy-flower-0bff4b703.1.azurestaticapps.net" -ForegroundColor White
}
if (-not $SkipApi) {
    Write-Host "API:      https://$FunctionAppName.azurewebsites.net" -ForegroundColor White
}
Write-Host ""
