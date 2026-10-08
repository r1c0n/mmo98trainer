param([Parameter(Mandatory = $true)][string]$GameDirectory, [switch]$Baseline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name MMO98 -ErrorAction SilentlyContinue) { throw 'Close MMO98 before running the smoke test.' }
$managedPath = Join-Path $gameRoot 'MMO98_Data\Managed'
& dotnet build (Join-Path $projectRoot 'tests\RuntimeSmoke\RuntimeSmoke.csproj') -c Release "-p:GameManagedPath=$managedPath" '--source=https://api.nuget.org/v3/index.json' '--source=https://nuget.bepinex.dev/v3/index.json'
if ($LASTEXITCODE -ne 0) { throw 'Smoke test build failed.' }
$workPath = Join-Path $projectRoot 'obj\runtime-smoke'
New-Item -ItemType Directory -Path $workPath -Force | Out-Null
$pluginPath = Join-Path $gameRoot 'BepInEx\plugins\mmo98trainer\mmo98trainer.dll'
$smokePath = Join-Path $gameRoot 'BepInEx\plugins\mmo98trainer.RuntimeSmoke.dll'
$configPath = Join-Path $gameRoot 'BepInEx\config\com.mmo98.trainer.cfg'
$appIdPath = Join-Path $gameRoot 'steam_appid.txt'
if (Test-Path -LiteralPath $smokePath) { throw "A smoke test DLL already exists: $smokePath" }
$pluginExisted = Test-Path -LiteralPath $pluginPath
$configExisted = Test-Path -LiteralPath $configPath
$appIdExisted = Test-Path -LiteralPath $appIdPath
$pluginBackup = Join-Path $workPath 'original-plugin.dll'
$configBackup = Join-Path $workPath 'original-config.cfg'
if ($pluginExisted) { Copy-Item -LiteralPath $pluginPath -Destination $pluginBackup -Force }
if ($configExisted) { Copy-Item -LiteralPath $configPath -Destination $configBackup -Force }
# The game is launched only into its main menu; the harness checks that no profile is loaded.
$profiles = Join-Path $env:USERPROFILE 'AppData\LocalLow\BiteMe Games\MMO98\profiles'
function Read-SaveHashes {
    if (Test-Path -LiteralPath $profiles) {
        Get-ChildItem -LiteralPath $profiles -Recurse -File | Where-Object { $_.Extension -in @('.gnome', '.bak') } |
            Sort-Object FullName | ForEach-Object { "$($_.FullName):$((Get-FileHash -LiteralPath $_.FullName).Hash)" }
    }
}
$saveHashesBefore = @(Read-SaveHashes)
$oldSmokeFlag = $env:MMO98_TRAINER_SMOKE_TEST
$gameProcess = $null
try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $pluginPath) -Force | Out-Null
    if (-not $Baseline) { Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\netstandard2.1\mmo98trainer.dll') -Destination $pluginPath -Force }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'tests\RuntimeSmoke\bin\Release\netstandard2.1\mmo98trainer.RuntimeSmoke.dll') -Destination $smokePath
    if (-not $appIdExisted) { [IO.File]::WriteAllText($appIdPath, '3907940') }
    $env:MMO98_TRAINER_SMOKE_TEST = if ($Baseline) { 'baseline' } else { '1' }
    $logPath = Join-Path $workPath $(if ($Baseline) { 'Baseline-Player.log' } else { 'Player.log' })
    $arguments = @('-batchmode', '-nographics', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + $logPath + '"'))
    $startedAt = [DateTime]::UtcNow
    $gameProcess = Start-Process -FilePath (Join-Path $gameRoot 'MMO98.exe') -ArgumentList $arguments -WorkingDirectory $gameRoot -WindowStyle Hidden -PassThru
    if (-not $gameProcess.WaitForExit(45000)) { throw 'Smoke test exceeded 45 seconds.' }
    $bepLog = Join-Path $gameRoot 'BepInEx\LogOutput.log'
    if ((Get-Item -LiteralPath $bepLog).LastWriteTimeUtc -lt $startedAt) { throw 'The game did not produce a fresh BepInEx log.' }
    $log = Get-Content -LiteralPath $bepLog -Raw
    Copy-Item -LiteralPath $bepLog -Destination (Join-Path $workPath $(if ($Baseline) { 'Baseline-BepInEx.log' } else { 'BepInEx.log' })) -Force
    $result = $log -split '\r?\n' | Where-Object { $_ -match 'TRAINER_SMOKE_(PASS|FAIL|BASELINE)' }
    Write-Output $result
    if ($log -match 'TRAINER_SMOKE_FAIL' -or ($log -notmatch 'TRAINER_SMOKE_PASS' -and -not $Baseline)) { throw "Runtime smoke test failed. See $workPath" }
    $playerLog = Get-Content -LiteralPath $logPath -Raw
    if ($playerLog -match 'Crash!!!' -or $gameProcess.ExitCode -ne 0) { throw "Native game shutdown failed (exit $($gameProcess.ExitCode)). Assertions are logged separately in $workPath." }
}
finally {
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) { Stop-Process -Id $gameProcess.Id -Force; $gameProcess.WaitForExit() }
    $env:MMO98_TRAINER_SMOKE_TEST = $oldSmokeFlag
    if ($pluginExisted) { Copy-Item -LiteralPath $pluginBackup -Destination $pluginPath -Force }
    elseif (Test-Path -LiteralPath $pluginPath) { Remove-Item -LiteralPath $pluginPath }
    if ($configExisted) { Copy-Item -LiteralPath $configBackup -Destination $configPath -Force }
    elseif (Test-Path -LiteralPath $configPath) { Remove-Item -LiteralPath $configPath }
    if (Test-Path -LiteralPath $smokePath) { Remove-Item -LiteralPath $smokePath }
    if (-not $appIdExisted -and (Test-Path -LiteralPath $appIdPath)) { Remove-Item -LiteralPath $appIdPath }
    $saveHashesAfter = @(Read-SaveHashes)
    if (($saveHashesBefore -join '\n') -ne ($saveHashesAfter -join '\n')) { throw 'Save file hashes changed during the smoke test.' }
    Write-Output 'Original plugin and config restored; save file hashes unchanged.'
}
