param(
    [string]$ArchivePath = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'SAIKI-charts-v1.zip')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$songId = '19b099bc-a1ee-4737-911c-d95f5354f1ec'
$dataRoot = Join-Path $env:USERPROFILE 'AppData/LocalLow/DefaultCompany/RhythmDojo'
$songsRoot = [IO.Path]::GetFullPath((Join-Path $dataRoot 'Songs'))
$backupRoot = Join-Path $dataRoot ('SongBackups/' + [Guid]::NewGuid().ToString('N'))
$stagingRoot = Join-Path $dataRoot ('SaikiImport-' + [Guid]::NewGuid().ToString('N'))
$chartPath = Join-Path $PSScriptRoot '../Content/SAIKI/project.rdchart.json'
$project = Get-Content -LiteralPath $chartPath -Raw | ConvertFrom-Json

# Validate and stage the complete import before changing the active song library.
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
try
{
    $audioEntry = $archive.GetEntry($songId + '/' + $project.song.audio)
    if ($null -eq $audioEntry)
    {
        throw 'The SAIKI archive is missing its audio.'
    }

    $stagedSong = Join-Path $stagingRoot $songId
    New-Item -ItemType Directory -Path (Join-Path $stagedSong 'audio') -Force | Out-Null
    Copy-Item -LiteralPath $chartPath -Destination (Join-Path $stagedSong 'project.rdchart.json')
    $audioPath = Join-Path $stagedSong $project.song.audio
    [IO.Compression.ZipFileExtensions]::ExtractToFile($audioEntry, $audioPath)
    $expectedHash = [IO.Path]::GetFileNameWithoutExtension($audioPath)
    if ((Get-FileHash -LiteralPath $audioPath -Algorithm SHA256).Hash -ine $expectedHash)
    {
        throw 'The SAIKI audio hash does not match the chart package.'
    }
}
finally
{
    $archive.Dispose()
}

New-Item -ItemType Directory -Path $songsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
foreach ($folder in Get-ChildItem -LiteralPath $songsRoot -Directory)
{
    $resolvedPath = [IO.Path]::GetFullPath($folder.FullName)
    if ([IO.Path]::GetDirectoryName($resolvedPath) -ine $songsRoot)
    {
        throw 'Song backup target is outside the Songs directory.'
    }

    if (($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    {
        throw 'Song backup does not support linked folders.'
    }

    Move-Item -LiteralPath $resolvedPath -Destination $backupRoot
}

Move-Item -LiteralPath $stagedSong -Destination $songsRoot
Remove-Item -LiteralPath $stagingRoot
Write-Output "Installed SAIKI. Previous user songs are backed up at $backupRoot"
