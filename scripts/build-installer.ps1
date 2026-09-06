param([string]$MakeNsis = "$env:LOCALAPPDATA\MailIntakeBuild\nsis-3.12\makensis.exe", [switch]$TestPackage)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseRoot = Join-Path $projectRoot 'artifacts\win-x64'
$generated = Join-Path $projectRoot 'artifacts\installer-build'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
if (!(Test-Path (Join-Path $releaseRoot 'MailIntake.exe'))) { throw 'Build the desktop release first.' }
$files = @(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File | Where-Object { $_.Name -notin @('Update.cmd','update-files.json') })
$install = [Collections.Generic.List[string]]::new()
$delete = [Collections.Generic.List[string]]::new()
$dirs = [Collections.Generic.HashSet[string]]::new()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($releaseRoot.Length + 1)
    $parent = Split-Path $relative -Parent
    $install.Add('SetOutPath "$INSTDIR\' + $parent + '"')
    $install.Add('File "' + $file.FullName + '"')
    $delete.Add('Delete "$INSTDIR\' + $relative + '"')
    while ($parent) { $null = $dirs.Add($parent); $parent = Split-Path $parent -Parent }
}
foreach ($dir in ($dirs | Sort-Object Length -Descending)) { $delete.Add('RMDir "$INSTDIR\' + $dir + '"') }
$installPath = Join-Path $generated 'install.nsh'
$deletePath = Join-Path $generated 'delete.nsh'
$install | Set-Content -LiteralPath $installPath -Encoding utf8
$delete | Set-Content -LiteralPath $deletePath -Encoding utf8
$output = Join-Path $projectRoot 'artifacts\MailIntake-Setup-1.0.0.exe'
$options = @('/V2', "/DOUTPUT=$output", "/DINSTALLFILES=$installPath", "/DDELETEFILES=$deletePath")
if ($TestPackage) {
    $output = Join-Path $generated 'MailIntake-TestSetup.exe'
    $options[1] = "/DOUTPUT=$output"
    $options += @('/DAPPKEY=MailIntakeInstallerTest','/DAPPNAME=MailIntake Installer Test')
}
& $MakeNsis @options /INPUTCHARSET UTF8 (Join-Path $PSScriptRoot 'installer.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
Get-FileHash -LiteralPath $output -Algorithm SHA256
