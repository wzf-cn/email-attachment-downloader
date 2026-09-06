$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $env:TEMP ('MailIntake-installer-' + [guid]::NewGuid().ToString('N'))
$installDir = Join-Path $testRoot 'program'
$testRegistry = 'HKCU:\Software\MailIntakeInstallerTest'
if (Test-Path $testRegistry) { throw 'An earlier installer test registration exists; inspect it first.' }
if (@(Get-Process MailIntake -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close the running application before installer tests.' }
$setup = Join-Path $projectRoot 'artifacts\installer-build\MailIntake-TestSetup.exe'
$env:MAILINTAKE_TEST_HOME = Join-Path $testRoot 'data'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
function Install-Test {
    $p = Start-Process -FilePath $setup -ArgumentList @('/S',"/D=$installDir") -PassThru -Wait -WindowStyle Hidden
    if ($p.ExitCode -ne 0 -or !(Test-Path (Join-Path $installDir 'Uninstall.exe'))) { throw 'Installation failed' }
}
Install-Test
if (!(Test-Path $testRegistry)) { throw 'Installation registry entry missing' }
$uninstallRegistry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MailIntakeInstallerTest'
if (!(Test-Path $uninstallRegistry)) { throw 'Windows uninstall entry missing' }
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'MailIntake Installer Test\MailIntake Installer Test.lnk'
if (!(Test-Path $shortcut)) { throw 'Start menu shortcut missing' }
$p = Start-Process -FilePath (Join-Path $installDir 'MailIntake.exe') -ArgumentList '--smoke-test' -PassThru -Wait -WindowStyle Hidden
if ($p.ExitCode -ne 0) { throw 'Installed app smoke test failed' }
$config = Join-Path $env:MAILINTAKE_TEST_HOME 'settings.json'
$before = (Get-FileHash -LiteralPath $config).Hash
Set-Content -LiteralPath (Join-Path $installDir 'user-download.txt') -Value 'preserve this user file'
Install-Test
if ((Get-FileHash -LiteralPath $config).Hash -ne $before) { throw 'Upgrade changed configuration' }
$lock=[Threading.Mutex]::new($true,'Local\KeywordMailDownloader')
try {
    $blocked=Start-Process -FilePath $setup -ArgumentList @('/S',"/D=$installDir") -PassThru -Wait -WindowStyle Hidden
    if ($blocked.ExitCode -eq 0) { throw 'Silent update did not refuse a running application' }
} finally { $lock.ReleaseMutex(); $lock.Dispose() }
$p = Start-Process -FilePath (Join-Path $installDir 'Uninstall.exe') -ArgumentList '/S' -PassThru -Wait -WindowStyle Hidden
if ($p.ExitCode -ne 0) { throw 'Uninstall failed' }
if (Test-Path (Join-Path $installDir 'MailIntake.exe')) { throw 'Program was not removed' }
if (!(Test-Path (Join-Path $installDir 'user-download.txt'))) { throw 'Uninstall removed user download' }
if ((Get-FileHash -LiteralPath $config).Hash -ne $before) { throw 'Uninstall changed configuration' }
if (Test-Path $testRegistry) { throw 'Uninstall registration was not removed' }
if (Test-Path $uninstallRegistry) { throw 'Windows uninstall entry was not removed' }
if (Test-Path $shortcut) { throw 'Start menu shortcut was not removed' }
'INSTALL_UPGRADE_UNINSTALL_DATA_PRESERVATION_OK'
"Test artifacts: $testRoot"
