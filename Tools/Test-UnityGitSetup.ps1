$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$engine = (Get-Process -Id $PID).Path
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('RhythmDojoGitTests-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Write-TestFile([string]$path, [string]$content) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $content.Replace("`r`n", "`n"), $utf8)
}
function Git([string[]]$arguments) {
    if ($PSVersionTable.PSVersion -lt [version]'7.3' -or $PSNativeCommandArgumentPassing -eq 'Legacy') {
        $arguments = @($arguments | ForEach-Object { $_.Replace('"', '\"') })
    }
    & git.exe @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "git failed: $arguments" }
}
function New-Case([string]$name) {
    $path = Join-Path $testRoot $name
    [void][IO.Directory]::CreateDirectory($path)
    Set-Location -LiteralPath $path
    Git @('init', '-q', '-b', 'main')
    Git @('config', 'user.name', 'Local setup verification')
    Git @('config', 'user.email', 'verification@example.invalid')
    Git @('config', 'core.autocrlf', 'false')
    return $path
}
function Check-Meta([bool]$shouldPass, [string]$expected = '') {
    $ErrorActionPreference = 'Continue'
    $output = & $engine -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'Tools/Test-UnityMeta.ps1') 2>&1
    $passed = $LASTEXITCODE -eq 0
    if ($passed -ne $shouldPass -or ($expected -and ($output -join "`n") -notlike "*$expected*")) {
        throw "Unexpected meta result: $output"
    }
}
Push-Location
try {
    foreach ($name in @('valid', 'missing', 'orphan', 'duplicate', 'move', 'empty', 'missing-folder', 'partial')) {
        $case = New-Case $name
        Write-TestFile (Join-Path $case 'Assets/A.cs') '// asset'
        Write-TestFile (Join-Path $case 'Assets/A.cs.meta') "fileFormatVersion: 2`nguid: 11111111111111111111111111111111`n"
        Git @('add', '.')
        switch ($name) {
            'missing' { Git @('rm', '--cached', 'Assets/A.cs.meta') }
            'orphan' { Git @('rm', '--cached', 'Assets/A.cs') }
            'duplicate' {
                Write-TestFile (Join-Path $case 'Assets/B.cs') '// asset B'
                Copy-Item -LiteralPath 'Assets/A.cs.meta' -Destination 'Assets/B.cs.meta'
                Git @('add', '.')
            }
            'move' {
                Move-Item -LiteralPath 'Assets/A.cs' -Destination 'Assets/Renamed.cs'
                Move-Item -LiteralPath 'Assets/A.cs.meta' -Destination 'Assets/Renamed.cs.meta'
                Git @('add', '-A')
            }
            'empty' {
                Write-TestFile (Join-Path $case 'Assets/Empty/.gitkeep') ''
                Write-TestFile (Join-Path $case 'Assets/Empty.meta') "fileFormatVersion: 2`nguid: 22222222222222222222222222222222`nfolderAsset: yes`n"
                Git @('add', '.')
            }
            'missing-folder' {
                Write-TestFile (Join-Path $case 'Assets/Empty/.gitkeep') ''
                Git @('add', '.')
            }
            'partial' {
                Write-TestFile (Join-Path $case 'Assets/A.cs.meta') "fileFormatVersion: 2`nguid: invalid-only-in-working-tree`n"
            }
        }
        switch ($name) {
            'missing' { Check-Meta $false 'Missing asset meta' }
            'orphan' { Check-Meta $false 'Orphan meta' }
            'duplicate' { Check-Meta $false 'Duplicate GUID' }
            'missing-folder' { Check-Meta $false 'Missing folder meta' }
            default { Check-Meta $true }
        }
        if ($name -eq 'missing') {
            [void][IO.Directory]::CreateDirectory((Join-Path $case 'Tools'))
            [void][IO.Directory]::CreateDirectory((Join-Path $case '.githooks'))
            Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Test-UnityMeta.ps1') -Destination 'Tools/Test-UnityMeta.ps1'
            Copy-Item -LiteralPath (Join-Path $projectRoot '.githooks/pre-commit') -Destination '.githooks/pre-commit'
            Git @('config', 'core.hooksPath', '.githooks')
            $ErrorActionPreference = 'Continue'
            $output = & git.exe commit -m 'Must be rejected' 2>&1
            $ErrorActionPreference = 'Stop'
            if ($LASTEXITCODE -eq 0 -or ($output -join "`n") -notlike '*Missing asset meta*') { throw 'Pre-commit hook did not reject invalid index.' }
        }
        Write-Host "PASS meta: $name"
    }
    Set-Location -LiteralPath $projectRoot
    $driver = & git.exe config --local --get merge.unityyamlmerge.driver
    if (!$driver) { throw 'Configure the project merge driver first.' }
    $prefab = Get-Content -LiteralPath 'Assets/Game/Prefabs/TapNote.prefab' -Raw
    $case = New-Case 'merge'
    Write-TestFile (Join-Path $case '.gitattributes') "*.prefab merge=unityyamlmerge`n"
    Write-TestFile (Join-Path $case 'Test.prefab') $prefab
    Git @('config', 'merge.unityyamlmerge.driver', $driver)
    Git @('add', '.')
    Git @('commit', '-qm', 'Base')
    Git @('switch', '-qc', 'incoming')
    Write-TestFile (Join-Path $case 'Test.prefab') $prefab.Replace('m_Layer: 0', 'm_Layer: 8')
    Git @('commit', '-qam', 'Incoming layer')
    Git @('switch', '-q', 'main')
    Write-TestFile (Join-Path $case 'Test.prefab') $prefab.Replace('m_Name: TapNote', 'm_Name: RenamedNote')
    Git @('commit', '-qam', 'Local name')
    Git @('merge', '--no-edit', 'incoming')
    $merged = Get-Content -LiteralPath 'Test.prefab' -Raw
    if ($merged -notmatch 'm_Name: RenamedNote' -or $merged -notmatch 'm_Layer: 8') { throw 'Merge lost an independent change.' }
    Write-Host 'PASS merge: independent changes retained'
    Git @('switch', '-qc', 'conflicting')
    Write-TestFile (Join-Path $case 'Test.prefab') $merged.Replace('m_Name: RenamedNote', 'm_Name: IncomingConflict')
    Git @('commit', '-qam', 'Incoming name conflict')
    Git @('switch', '-q', 'main')
    Write-TestFile (Join-Path $case 'Test.prefab') $merged.Replace('m_Name: RenamedNote', 'm_Name: LocalConflict')
    Git @('commit', '-qam', 'Local name conflict')
    $ErrorActionPreference = 'Continue'
    $output = & git.exe merge --no-edit conflicting 2>&1
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -eq 0) { throw 'Conflicting merge unexpectedly succeeded.' }
    if (!(& git.exe ls-files --unmerged)) { throw 'Conflict not retained in the index.' }
    Write-Host 'PASS merge: unresolved conflict retained'
    Write-Host "All setup checks passed. Isolated fixtures retained at $testRoot"
}
finally { Pop-Location }
