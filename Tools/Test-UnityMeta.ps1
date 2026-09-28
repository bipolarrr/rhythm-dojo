param([string]$Repository = '.')
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
Push-Location -LiteralPath $Repository
try {
    $root = & git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0) { throw 'Not a Git repository.' }
    Set-Location -LiteralPath $root
    $unmerged = & git ls-files --unmerged
    if ($LASTEXITCODE -ne 0 -or $unmerged) { throw 'Resolve and stage all merge conflicts before committing.' }
    $raw = (& git ls-files --cached -z -- Assets) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the Git index.' }
    $paths = @($raw.Split([char]0) | Where-Object { $_ })
    $files = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $folders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($path in $paths) {
        [void]$files.Add($path)
        $parent = $path
        while ($parent.Contains('/')) {
            $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
            if ($parent -ne 'Assets') { [void]$folders.Add($parent) }
        }
    }
    $issues = New-Object 'System.Collections.Generic.List[string]'
    $guids = @{}
    # Dot files (including .gitkeep), hidden directories and Unity-ignored files need no meta.
    function IsIgnoredByUnity([string]$path) {
        foreach ($part in $path.Split('/')) {
            if ($part.StartsWith('.') -or $part.EndsWith('~')) { return $true }
        }
        return $path.EndsWith('.tmp', [StringComparison]::OrdinalIgnoreCase)
    }
    foreach ($folder in $folders) {
        if (!(IsIgnoredByUnity $folder) -and !$files.Contains($folder + '.meta')) {
            $issues.Add("Missing folder meta: $folder.meta")
        }
    }
    foreach ($path in $paths) {
        if (IsIgnoredByUnity $path) { continue }
        if (!$path.EndsWith('.meta', [StringComparison]::Ordinal)) {
            if (!$files.Contains($path + '.meta')) { $issues.Add("Missing asset meta: $path.meta") }
            continue
        }
        $target = $path.Substring(0, $path.Length - 5)
        if (!$files.Contains($target) -and !$folders.Contains($target)) { $issues.Add("Orphan meta: $path") }
        $content = (& git show (':' + $path)) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "Cannot read staged meta: $path" }
        $matchesFound = [regex]::Matches($content, '(?m)^guid: ([0-9a-fA-F]{32})\r?$')
        if ($matchesFound.Count -ne 1) { $issues.Add("Expected one valid GUID: $path"); continue }
        $guid = $matchesFound[0].Groups[1].Value.ToLowerInvariant()
        if ($guid -eq ('0' * 32)) { $issues.Add("Empty GUID: $path") }
        if ($guids.ContainsKey($guid)) { $issues.Add("Duplicate GUID: $path and $($guids[$guid])") }
        else { $guids[$guid] = $path }
    }
    if ($issues.Count) { throw ($issues -join "`n") }
    Write-Host "Unity meta validation passed ($($guids.Count) staged meta files)."
}
finally { Pop-Location }
