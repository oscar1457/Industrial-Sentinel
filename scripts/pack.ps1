param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$CreateInstaller
)

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\IndustrialSentinel.App\IndustrialSentinel.App.csproj"
$distDir = Join-Path $root "dist"
$publishDir = Join-Path $distDir "IndustrialSentinel"

[xml]$xml = Get-Content $project
$version = ($xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = "1.0.0" }

& "$PSScriptRoot\publish.ps1" -Configuration $Configuration -Runtime $Runtime -SelfContained:$SelfContained -PublishDir $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$zipPath = Join-Path $distDir "IndustrialSentinel-$version-$Runtime.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath
Write-Host "Created $zipPath"

if ($CreateInstaller) {
    $iscc = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        Write-Error "Inno Setup (ISCC.exe) not found. Install Inno Setup 6 to build the installer."
        exit 1
    }

    $iss = Join-Path $root "install\IndustrialSentinel.iss"
    & $iscc "/DMyAppVersion=$version" "/DMyAppSourceDir=$publishDir" "/DMyAppOutputDir=$distDir" $iss
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
