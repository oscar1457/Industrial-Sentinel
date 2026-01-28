param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$PublishDir = ""
)

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\IndustrialSentinel.App\IndustrialSentinel.App.csproj"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $root "dist\IndustrialSentinel"
}

$sc = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing to $PublishDir (SelfContained=$sc)"

& dotnet publish $project -c $Configuration -r $Runtime -p:SelfContained=$sc -p:PublishSingleFile=false -p:PublishTrimmed=false -o $PublishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
