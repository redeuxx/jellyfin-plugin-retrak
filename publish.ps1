#Requires -Version 5.1
<#
.SYNOPSIS
    Build the ReTrak Jellyfin plugin and publish it to GitHub.

.DESCRIPTION
    Updates version metadata, builds ReTrak.dll and a JPRM catalog zip, updates
    manifest-release/manifest.json, then optionally commits, tags, pushes, and
    creates a GitHub release.

.PARAMETER Version
    Plugin version, e.g. 1.0.2 or 1.0.2.0. Git tags use the first three parts
    (v1.0.2). Assembly and catalog versions use four parts (1.0.2.0).

.PARAMETER Changelog
    Short changelog line stored in build.yaml and the plugin catalog manifest.

.PARAMETER ReleaseNotes
    GitHub release notes text. Defaults to the changelog when omitted.

.PARAMETER ReleaseNotesFile
    Path to a markdown file used as GitHub release notes.

.PARAMETER SkipPublish
    Build and update manifest files locally without git commit, push, tag, or
    GitHub release.

.PARAMETER Force
    Allow creating a release even if the git tag already exists locally.

.EXAMPLE
    .\publish.ps1 -Version 1.0.2 -Changelog "Fix episode sync for multi-user setups"

.EXAMPLE
    .\publish.ps1 -Version 1.0.2 -Changelog "Fix episode sync" -ReleaseNotesFile .\notes.md

.EXAMPLE
    .\publish.ps1 -Version 1.0.2 -Changelog "Test build" -SkipPublish
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Changelog,

    [string]$ReleaseNotes,

    [string]$ReleaseNotesFile,

    [switch]$SkipPublish,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$githubOwner = 'redeuxx'
$githubRepo = 'jellyfin-plugin-retrak'
$githubRepoSlug = "$githubOwner/$githubRepo"
$manifestPath = Join-Path $repoRoot 'manifest-release/manifest.json'
$buildYamlPath = Join-Path $repoRoot 'build.yaml'
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$outputDir = Join-Path $repoRoot 'dist'
$dllName = 'ReTrak.dll'
$logoUrl = "https://raw.githubusercontent.com/$githubRepoSlug/master/logo.jpg"

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Normalize-Version {
    param([string]$InputVersion)

    $clean = $InputVersion.Trim().TrimStart('v', 'V')
    $parts = $clean -split '\.'
    if ($parts.Count -lt 3 -or $parts.Count -gt 4) {
        throw "Version must have 3 or 4 numeric parts (e.g. 1.0.2 or 1.0.2.0). Got: $InputVersion"
    }

    foreach ($part in $parts) {
        if ($part -notmatch '^\d+$') {
            throw "Version parts must be numeric. Got: $InputVersion"
        }
    }

    while ($parts.Count -lt 4) {
        $parts += '0'
    }

    [pscustomobject]@{
        Full      = ($parts -join '.')
        Tag       = "v$($parts[0]).$($parts[1]).$($parts[2])"
        ZipBase   = "retrak_$($parts -join '.')"
        Short     = "$($parts[0]).$($parts[1]).$($parts[2])"
    }
}

function Get-JprmExecutable {
    $command = Get-Command jprm -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $searchRoots = @(
        (Join-Path $env:APPDATA 'Python')
        (Join-Path $env:LOCALAPPDATA 'Programs\Python')
    )

    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $candidate = Get-ChildItem -Path $root -Filter 'jprm.exe' -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw @(
        'JPRM was not found. Install it with: pip install jprm',
        'Then ensure jprm is on PATH or rerun this script.'
    ) -join [Environment]::NewLine
}

function Assert-CommandExists {
    param(
        [string]$Name,
        [string]$InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required but was not found. $InstallHint"
    }
}

function Update-DirectoryBuildProps {
    param(
        [string]$Path,
        [string]$FullVersion
    )

    $content = Get-Content -Path $Path -Raw
    $content = $content -replace '(<Version>)[^<]+(</Version>)', "`${1}$FullVersion`${2}"
    $content = $content -replace '(<AssemblyVersion>)[^<]+(</AssemblyVersion>)', "`${1}$FullVersion`${2}"
    $content = $content -replace '(<FileVersion>)[^<]+(</FileVersion>)', "`${1}$FullVersion`${2}"
    Set-Content -Path $Path -Value $content -NoNewline
}

function Update-BuildYaml {
    param(
        [string]$Path,
        [string]$FullVersion,
        [string]$ChangeLogText
    )

    $content = Get-Content -Path $Path -Raw
    $content = $content -replace '(?m)^version:\s*".*"', "version: `"$FullVersion`""
    $content = $content -replace '(?m)^changelog:\s*\|-(?:\r?\n(?:  .*)*)*', ("changelog: |-`r`n  - $ChangeLogText")
    Set-Content -Path $Path -Value $content -NoNewline
}

function Repair-Manifest {
    param(
        [string]$ManifestPath,
        [string]$FullVersion,
        [string]$ChangelogText,
        [string]$ZipPath,
        [string]$ImageUrl,
        [string]$PluginDownloadUrl
    )

    $retrakDir = Join-Path (Split-Path $ManifestPath -Parent) 'retrak'
    if (Test-Path $retrakDir) {
        Remove-Item -Recurse -Force $retrakDir
    }

    $hash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksum = "sha256:$hash"
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

    $manifest = Get-Content -Path $ManifestPath -Raw | ConvertFrom-Json
    $plugin = $manifest[0]

    $plugin.imageUrl = $ImageUrl
    if ($plugin.PSObject.Properties.Match('image').Count -gt 0) {
        $plugin.PSObject.Properties.Remove('image')
    }

    $versionEntry = @($plugin.versions | Where-Object { $_.version -eq $FullVersion })[0]
    if (-not $versionEntry) {
        throw "Manifest does not contain version $FullVersion after jprm repo add."
    }

    $versionEntry.changelog = $ChangelogText
    $versionEntry.sourceUrl = $PluginDownloadUrl
    $versionEntry.checksum = $checksum
    $versionEntry.timestamp = $timestamp

    $json = $manifest | ConvertTo-Json -Depth 10
    Set-Content -Path $ManifestPath -Value $json -NoNewline

    return $checksum
}

function Get-ReleaseNotesText {
    param(
        [string]$DefaultNotes,
        [string]$Notes,
        [string]$NotesFile
    )

    if ($NotesFile) {
        if (-not (Test-Path $NotesFile)) {
            throw "Release notes file not found: $NotesFile"
        }
        return Get-Content -Path $NotesFile -Raw
    }

    if ($Notes) {
        return $Notes
    }

    return $DefaultNotes
}

$versionInfo = Normalize-Version -InputVersion $Version
$zipFileName = "$($versionInfo.ZipBase).zip"
$zipPath = Join-Path $outputDir $zipFileName
$dllPath = Join-Path $outputDir $dllName
$pluginDownloadUrl = "https://github.com/$githubRepoSlug/releases/download/$($versionInfo.Tag)/$zipFileName"
$releaseNotesPath = Join-Path $outputDir "release-notes-$($versionInfo.Tag).md"
$releaseNotesText = Get-ReleaseNotesText -DefaultNotes $Changelog -Notes $ReleaseNotes -NotesFile $ReleaseNotesFile

Write-Host 'ReTrak Jellyfin plugin publish' -ForegroundColor Green
Write-Host "  Version:      $($versionInfo.Full)"
Write-Host "  Git tag:      $($versionInfo.Tag)"
Write-Host "  Catalog zip:  $zipFileName"
Write-Host "  Changelog:    $Changelog"

Write-Step 'Checking prerequisites'
Assert-CommandExists -Name 'dotnet' -InstallHint 'Install the .NET SDK.'
Assert-CommandExists -Name 'git' -InstallHint 'Install Git.'
if (-not $SkipPublish) {
    Assert-CommandExists -Name 'gh' -InstallHint 'Install GitHub CLI and run gh auth login.'
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated. Run: gh auth login'
    }
}

$jprmExe = Get-JprmExecutable
Write-Host "Using JPRM: $jprmExe"

if (-not (Test-Path $manifestPath)) {
    Write-Step 'Initializing plugin catalog manifest'
    & $jprmExe repo init (Split-Path $manifestPath -Parent)
    if ($LASTEXITCODE -ne 0) {
        throw 'jprm repo init failed.'
    }
}

if (-not $SkipPublish) {
    $existingTag = git -C $repoRoot tag --list $versionInfo.Tag
    if ($existingTag -and -not $Force) {
        throw "Git tag $($versionInfo.Tag) already exists. Use -Force to override or pick a new version."
    }

    $remoteTag = gh release view $versionInfo.Tag --repo $githubRepoSlug 2>$null
    if ($remoteTag -and -not $Force) {
        throw "GitHub release $($versionInfo.Tag) already exists. Use -Force or pick a new version."
    }
}

Write-Step 'Updating version metadata'
Update-DirectoryBuildProps -Path $propsPath -FullVersion $versionInfo.Full
Update-BuildYaml -Path $buildYamlPath -FullVersion $versionInfo.Full -ChangeLogText $Changelog

Write-Step 'Building plugin and JPRM package'
if (Test-Path $outputDir) {
    Get-ChildItem -Path $outputDir -Filter "$($versionInfo.ZipBase)*" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

Push-Location $repoRoot
try {
    & $jprmExe plugin build -o $outputDir -v $versionInfo.Full
    if ($LASTEXITCODE -ne 0) {
        throw 'jprm plugin build failed.'
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $zipPath)) {
    throw "Expected plugin zip was not created: $zipPath"
}

if (-not (Test-Path $dllPath)) {
    throw "Expected plugin DLL was not created: $dllPath"
}

Write-Step 'Updating plugin catalog manifest'
& $jprmExe repo add `
    --plugin-url $pluginDownloadUrl `
    $manifestPath `
    $zipPath
if ($LASTEXITCODE -ne 0) {
    throw 'jprm repo add failed.'
}

$checksum = Repair-Manifest `
    -ManifestPath $manifestPath `
    -FullVersion $versionInfo.Full `
    -ChangelogText $Changelog `
    -ZipPath $zipPath `
    -ImageUrl $logoUrl `
    -PluginDownloadUrl $pluginDownloadUrl

Set-Content -Path $releaseNotesPath -Value $releaseNotesText -NoNewline
Write-Host "Package checksum: $checksum"

Write-Step 'Publish summary'
Write-Host "  DLL:       $dllPath"
Write-Host "  Zip:       $zipPath"
Write-Host "  Manifest:  $manifestPath"
Write-Host "  Notes:     $releaseNotesPath"
Write-Host "  Download:  $pluginDownloadUrl"
Write-Host ''
Write-Host 'Catalog URL for Jellyfin repositories:' -ForegroundColor Yellow
Write-Host "  https://raw.githubusercontent.com/$githubRepoSlug/master/manifest-release/manifest.json"

if ($SkipPublish) {
    Write-Host ''
    Write-Host 'SkipPublish was set. Local build and manifest update complete.' -ForegroundColor Green
    exit 0
}

if (-not $PSCmdlet.ShouldProcess($githubRepoSlug, "commit, tag $($versionInfo.Tag), push, and create GitHub release")) {
    Write-Host 'Publish cancelled.' -ForegroundColor Yellow
    exit 0
}

Write-Step 'Committing release files'
git -C $repoRoot add $propsPath $buildYamlPath $manifestPath
git -C $repoRoot diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git -C $repoRoot commit -m "Release $($versionInfo.Full): $Changelog"
    if ($LASTEXITCODE -ne 0) {
        throw 'git commit failed.'
    }
}
else {
    Write-Host 'No staged changes to commit.'
}

Write-Step "Creating git tag $($versionInfo.Tag)"
if ($Force) {
    git -C $repoRoot tag -d $versionInfo.Tag 2>$null | Out-Null
}
git -C $repoRoot tag -a $versionInfo.Tag -m "Release $($versionInfo.Full)"
if ($LASTEXITCODE -ne 0) {
    throw 'git tag failed.'
}

Write-Step 'Pushing commit and tag to origin'
git -C $repoRoot push origin HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'git push failed.'
}

git -C $repoRoot push origin $versionInfo.Tag
if ($LASTEXITCODE -ne 0) {
    throw 'git push --tags failed.'
}

Write-Step "Creating GitHub release $($versionInfo.Tag)"
if ($Force) {
    gh release delete $versionInfo.Tag --repo $githubRepoSlug --yes 2>$null | Out-Null
}

gh release create $versionInfo.Tag `
    --repo $githubRepoSlug `
    --title $versionInfo.Tag `
    --notes-file $releaseNotesPath `
    "$dllPath#$dllName" `
    "$zipPath#$zipFileName"
if ($LASTEXITCODE -ne 0) {
    throw 'gh release create failed.'
}

$releaseUrl = gh release view $versionInfo.Tag --repo $githubRepoSlug --json url -q .url
Write-Host ''
Write-Host 'Publish complete.' -ForegroundColor Green
Write-Host "Release: $releaseUrl"

exit 0
