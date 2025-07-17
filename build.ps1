#!/usr/bin/env pwsh

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "publish"
)

# Build for multiple platforms
$platforms = @("win-x64", "linux-x64", "osx-x64")

Write-Host "Building AI CLI for multiple platforms..." -ForegroundColor Green

# Clean previous builds
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}

foreach ($platform in $platforms) {
    Write-Host "Building for $platform..." -ForegroundColor Yellow
    
    $publishPath = Join-Path $OutputPath $platform
    
    dotnet publish src/ai-cli/ai-cli.csproj `
        --configuration $Configuration `
        --runtime $platform `
        --self-contained true `
        --output $publishPath `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=true `
        /p:TrimmerDefaultAction=link
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $platform"
        exit 1
    }
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output directory: $OutputPath"