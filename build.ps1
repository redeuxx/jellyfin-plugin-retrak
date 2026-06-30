#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$outputDir = Join-Path $repoRoot 'dist'
$project = Join-Path $repoRoot 'ReTrak\ReTrak.csproj'
$dllName = 'ReTrak.dll'

Write-Host 'Building ReTrak Jellyfin plugin (Release)...' -ForegroundColor Cyan

dotnet publish $project `
    --configuration Release `
    --output $outputDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error 'Build failed.'
    exit $LASTEXITCODE
}

$outputFile = Join-Path $outputDir $dllName
$resolved = (Resolve-Path $outputFile).Path

Write-Host ''
Write-Host 'Build succeeded.' -ForegroundColor Green
Write-Host "Output file: $resolved" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Copy this DLL into your Jellyfin plugins folder, for example:' -ForegroundColor DarkGray
Write-Host '  <jellyfin-data>/plugins/ReTrak/ReTrak.dll' -ForegroundColor DarkGray
Write-Host 'Then restart Jellyfin.' -ForegroundColor DarkGray

exit 0
