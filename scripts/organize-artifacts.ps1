param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if (!(Test-Path -LiteralPath $artifactRoot)) { return }
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/MailIntake.Desktop/MailIntake.Desktop.csproj') -Raw
$version = [version]$project.Project.PropertyGroup.Version
$moves = @()
foreach ($file in Get-ChildItem -LiteralPath $artifactRoot -File) {
    $relative = $null
    if ($file.Name -match '^MailIntake-Setup-(\d+\.\d+\.\d+)\.exe$' -and [version]$Matches[1] -lt $version) {
        $relative = 'archive/' + $Matches[1] + '/' + $file.Name
    } elseif ($file.Extension -eq '.png') {
        $group = $file.BaseName.Split('.')[0]
        $relative = 'screenshots/' + $group + '/' + $file.Name
    }
    if (!$relative) { continue }
    $destination = [IO.Path]::GetFullPath((Join-Path $artifactRoot $relative))
    $boundary = $artifactRoot + [IO.Path]::DirectorySeparatorChar
    if (!$file.FullName.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped artifacts directory' }
    foreach ($path in @($file.FullName,$destination)) {
        $cursor=$path
        while ($cursor.Length -ge $artifactRoot.Length) {
            if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Reparse point requires manual inspection: ' + $cursor) }
            $cursor=Split-Path $cursor -Parent
        }
    }
    if (Test-Path -LiteralPath $destination) { throw ('Destination already exists: ' + $relative) }
    $moves += [pscustomobject]@{Source=$file.FullName;Destination=$destination;SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
}
if (!$Apply) { $moves | Select-Object Source,Destination; Write-Output ('Planned moves: ' + $moves.Count); return }
# Record the complete plan before any move. No recursive deletion or directory moves.
$recordRoot = Join-Path $artifactRoot 'organization'
New-Item -ItemType Directory -Path $recordRoot -Force | Out-Null
$record = Join-Path $recordRoot ('moves-' + (Get-Date -Format 'yyyyMMdd-HHmmss-ffff') + '.json')
ConvertTo-Json -InputObject @($moves) -Depth 3 | Set-Content -LiteralPath $record -Encoding utf8
foreach ($move in $moves) {
    New-Item -ItemType Directory -Path (Split-Path $move.Destination -Parent) -Force | Out-Null
    Move-Item -LiteralPath $move.Source -Destination $move.Destination
    if ((Get-FileHash -LiteralPath $move.Destination -Algorithm SHA256).Hash -ne $move.SHA256) { throw ('Hash mismatch: ' + $move.Destination) }
}
Write-Output ('Moved and verified: ' + $moves.Count)
Write-Output ('Movement record: ' + $record)
