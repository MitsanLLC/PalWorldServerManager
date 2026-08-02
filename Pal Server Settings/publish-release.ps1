param(
    [string]$Project = ".\PalworldServerManager.csproj"
)

$ErrorActionPreference = "Stop"

$releaseRoot = Join-Path $PSScriptRoot "release"
$publishDir = Join-Path $releaseRoot "publish"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Publishing Pal Server Hub v0.9.0..." -ForegroundColor Cyan

dotnet restore $Project

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

$exe = Join-Path $publishDir "PalServerHub.exe"

if (!(Test-Path $exe)) {
    throw "Publish completed, but PalServerHub.exe was not found. Confirm <AssemblyName>PalServerHub</AssemblyName> is set in the project file."
}

Write-Host ""
Write-Host "Publish complete:" -ForegroundColor Green
Write-Host $publishDir
Write-Host ""
Write-Host "Standalone executable:" -ForegroundColor Green
Write-Host $exe
Write-Host ""
Write-Host "Test this executable outside Visual Studio before compiling installer.iss."
