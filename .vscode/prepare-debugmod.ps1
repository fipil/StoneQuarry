$ErrorActionPreference = "Stop"

$stage = Join-Path $PSScriptRoot "..\.debugmods\StoneQuarry"
$stage = [System.IO.Path]::GetFullPath($stage)

New-Item -ItemType Directory -Force -Path $stage | Out-Null

$buildOut = Join-Path $PSScriptRoot "..\bin\Debug\net10.0"
$dll = Join-Path $buildOut "StoneQuarry.dll"
$pdb = Join-Path $buildOut "StoneQuarry.pdb"
$modinfo = Join-Path $PSScriptRoot "..\modinfo.json"

Copy-Item $dll -Destination $stage -Force
if (Test-Path $pdb) {
    Copy-Item $pdb -Destination $stage -Force
}
Copy-Item $modinfo -Destination $stage -Force
