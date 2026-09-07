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
    Copy-Item -LiteralPath LICENSE -Destination artifacts/win-x64/LICENSE
    if(Test-Path -LiteralPath support){New-Item -ItemType Directory -Path artifacts/win-x64/support -Force | Out-Null; Copy-Item -Path support/* -Destination artifacts/win-x64/support -Recurse -Force}
    Copy-Item -LiteralPath THIRD_PARTY_NOTICES.md -Destination artifacts/win-x64/THIRD_PARTY_NOTICES.md
    Copy-Item -LiteralPath licenses -Destination artifacts/win-x64/licenses -Recurse -Force
    Set-Content -LiteralPath artifacts/win-x64/Update.cmd -Value '@start "" "%~dp0MailIntake.exe" --update' -Encoding ascii
    $releaseRoot = (Resolve-Path artifacts/win-x64).Path
    $updateFiles = @(Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | Where-Object { $_.Name -ne 'update-files.json' } | ForEach-Object { $_.FullName.Substring($releaseRoot.Length + 1) })
    $updateFiles += 'update-files.json'
    ConvertTo-Json -InputObject $updateFiles | Set-Content -LiteralPath artifacts/win-x64/update-files.json -Encoding utf8
    Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/MailIntake-win-x64.zip -Force
    Get-FileHash -LiteralPath artifacts/MailIntake-win-x64.zip -Algorithm SHA256
} finally { Pop-Location }
