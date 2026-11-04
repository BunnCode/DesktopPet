# Builds, tests and publishes DesktopPet, then copies the repo's data\ folder next to the published exe.
# Usage: powershell -ExecutionPolicy Bypass -File tools\publish\publish.ps1 [-SkipTests] [-Mirror]
#   -SkipTests  skip dotnet test
#   -Mirror     make publish\data an exact copy of data\ (also deletes files only the publish copy has)
# The old publish\data is always backed up to %TEMP%\DesktopPet-backups\ first.
param([switch]$SkipTests, [switch]$Mirror)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$publish = Join-Path $repo 'publish'
$data = Join-Path $repo 'data'

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Run($exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe $($arguments -join ' ') failed (exit $LASTEXITCODE)" }
}

# A running pet locks bin\ (or publish\ when it was started from there).
Step 'Stopping running pets'
$procs = @(Get-Process DesktopPet.App -ErrorAction SilentlyContinue)
$running = @($procs | ForEach-Object { $_.Path })  # read before they exit; Path is gone afterwards
foreach ($p in $procs) { Write-Host "stopping $($p.Id) $($p.Path)"; Stop-Process -Id $p.Id -Force }
if ($running.Count -gt 0) { Start-Sleep -Milliseconds 800 } else { Write-Host 'none running' }

Step 'Build'
Run dotnet @('build', (Join-Path $repo 'DesktopPet.slnx'), '-nologo')

if (-not $SkipTests) {
    Step 'Test'
    Run dotnet @('test', (Join-Path $repo 'DesktopPet.slnx'), '-nologo')
}

# Same arguments as the VS Code 'publish' task.
Step 'Publish'
Run dotnet @('publish', (Join-Path $repo 'src\DesktopPet.App\DesktopPet.App.csproj'), '-nologo',
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $publish)

Step 'Copying data'
if (-not (Test-Path $data)) { throw "No data folder at $data" }
$target = Join-Path $publish 'data'
if (Test-Path $target) {
    $backup = Join-Path $env:TEMP ("DesktopPet-backups\publish-data-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    Copy-Item (Join-Path $target '*') $backup -Recurse -Force
    Write-Host "backed up old publish\data to $backup"
}
$mode = if ($Mirror) { '/MIR' } else { '/E' }
& robocopy $data $target $mode /NFL /NDL /NJH /NP /R:2 /W:1
if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
$global:LASTEXITCODE = 0

# Files that only the publish copy still has (kept unless -Mirror).
$extra = Get-ChildItem $target -Recurse -File | ForEach-Object { $_.FullName.Substring($target.Length + 1) } |
    Where-Object { -not (Test-Path (Join-Path $data $_)) }
if ($extra) { Write-Host "`nIn publish\data but not in data\ (kept):" -ForegroundColor Yellow; $extra | ForEach-Object { "  $_" } }

Step 'Done'
Write-Host "exe:  $(Join-Path $publish 'DesktopPet.App.exe')"
Write-Host "data: $target"
if ($running.Count -gt 0) { Write-Host "stopped: $($running -join ', ')" }
