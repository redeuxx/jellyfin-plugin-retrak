#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$outputDir = Join-Path $repoRoot 'dist'
$project = Join-Path $repoRoot 'ReTrak\ReTrak.csproj'
$dllName = 'ReTrak.dll'
$builtDll = Join-Path $repoRoot "ReTrak\bin\Release\net9.0\$dllName"

Write-Host 'Building ReTrak Jellyfin plugin (Release)...' -ForegroundColor Cyan

dotnet build $project `
    --configuration Release `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error 'Build failed.'
    exit $LASTEXITCODE
}

if (-not (Test-Path $builtDll)) {
    throw "Expected build output was not found: $builtDll"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Copy-Item -Path $builtDll -Destination (Join-Path $outputDir $dllName) -Force

$resolved = (Resolve-Path (Join-Path $outputDir $dllName)).Path

Write-Host ''
Write-Host 'Build succeeded.' -ForegroundColor Green
Write-Host "Output file: $resolved" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Copy ONLY ReTrak.dll into your Jellyfin plugins folder:' -ForegroundColor DarkGray
Write-Host '  <jellyfin-data>/plugins/ReTrak/<version>/ReTrak.dll' -ForegroundColor DarkGray
Write-Host 'Do not copy other DLLs from dist/; Jellyfin provides those at runtime.' -ForegroundColor DarkGray
Write-Host 'Prefer installing from the catalog release zip (ReTrak.dll + meta.json).' -ForegroundColor DarkGray
Write-Host 'Then restart Jellyfin.' -ForegroundColor DarkGray

exit 0
