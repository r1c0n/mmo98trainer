param([Parameter(Mandatory = $true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managedPath = Join-Path $GameDirectory 'MMO98_Data\Managed'
$project = Join-Path $projectRoot 'mmo98trainer.csproj'
& dotnet build $project -c Release "-p:GameManagedPath=$managedPath"
if ($LASTEXITCODE -ne 0) { throw 'Trainer build failed.' }
$verification = Join-Path $projectRoot 'tests\Verification\Verification.csproj'
$plugin = Join-Path $projectRoot 'bin\Release\netstandard2.1\mmo98trainer.dll'
& dotnet run --project $verification -c Release -- $managedPath $plugin
if ($LASTEXITCODE -ne 0) { throw 'Trainer verification failed.' }
