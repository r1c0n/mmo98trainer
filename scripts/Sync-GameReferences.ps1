param([Parameter(Mandatory = $true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managedPath = Join-Path $GameDirectory 'MMO98_Data\Managed'
$references = @('Assembly-CSharp.dll', 'R3.dll', 'Unity.InputSystem.dll', 'SourceGeneration.dll', 'ObservableCollections.dll')
foreach ($name in $references) {
    if (-not (Test-Path -LiteralPath (Join-Path $managedPath $name))) { throw "Missing game reference: $name in $managedPath" }
}
$destination = Join-Path $projectRoot 'libs'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in $references) { Copy-Item -LiteralPath (Join-Path $managedPath $name) -Destination (Join-Path $destination $name) -Force }
Write-Output 'Synced five references from the installed game. New reference files remain ignored by Git.'
