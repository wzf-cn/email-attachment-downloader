param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    & $Dotnet restore src/MailIntake.Desktop/MailIntake.Desktop.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    & $Dotnet run --project tests/MailIntake.Tests/MailIntake.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $Dotnet publish src/MailIntake.Desktop/MailIntake.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath README.md -Destination artifacts/win-x64/README.md
    Copy-Item -LiteralPath THIRD_PARTY_NOTICES.md -Destination artifacts/win-x64/THIRD_PARTY_NOTICES.md
    Copy-Item -LiteralPath licenses -Destination artifacts/win-x64/licenses -Recurse -Force
    Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/MailIntake-win-x64.zip -Force
    Get-FileHash -LiteralPath artifacts/MailIntake-win-x64.zip -Algorithm SHA256
} finally { Pop-Location }
