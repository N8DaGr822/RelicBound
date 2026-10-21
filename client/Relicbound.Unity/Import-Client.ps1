param([Parameter(Mandatory = $true)][string]$UnityProject)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $UnityProject).Path
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings'))) {
    throw 'Choose an existing Unity Universal 3D project.'
}
$destination = Join-Path $project 'Assets/Relicbound'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets/Scripts/ArenaClient.cs') -Destination $destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../src/Relicbound.Contracts/WorldMessages.cs') -Destination $destination
Write-Output "Imported the client and shared contracts into $destination"
