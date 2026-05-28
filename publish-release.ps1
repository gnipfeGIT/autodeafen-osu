param(
    [string]$Output = "dist"
)

$ErrorActionPreference = "Stop"

dotnet publish .\AutoDeafenOsu.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --source https://api.nuget.org/v3/index.json `
    -o $Output `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:PublishTrimmed=false `
    /p:PublishReadyToRun=false `
    /p:DebugType=none `
    /p:DebugSymbols=false

Write-Host ""
Write-Host "Release executable:"
Write-Host (Resolve-Path (Join-Path $Output "AutoDeafenOsu.exe"))
