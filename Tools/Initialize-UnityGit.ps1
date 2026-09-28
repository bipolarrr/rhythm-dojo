param([string]$UnityYamlMergePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    & git rev-parse --git-dir *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Run this script inside a clone of the GitHub repository.' }
    $versionText = Get-Content -LiteralPath 'ProjectSettings/ProjectVersion.txt' -Raw
    $version = [regex]::Match($versionText, '(?m)^m_EditorVersion: (\S+)').Groups[1].Value
    if (!$version) { throw 'Cannot determine the Unity project version.' }
    if (!$UnityYamlMergePath) {
        $UnityYamlMergePath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Data/Tools/UnityYAMLMerge.exe"
    }
    if (!(Test-Path -LiteralPath $UnityYamlMergePath -PathType Leaf)) {
        throw "UnityYAMLMerge not found for $version. Supply -UnityYamlMergePath with the matching editor's executable."
    }
    $executable = (Resolve-Path -LiteralPath $UnityYamlMergePath).Path.Replace('\', '/')
    $hooks = & git config --get core.hooksPath
    if ($hooks -and $hooks -ne '.githooks') { throw "Existing hooksPath '$hooks' must be integrated, not overwritten." }
    if ((Get-Content ProjectSettings/EditorSettings.asset -Raw) -notmatch 'm_SerializationMode: 2') {
        throw 'Enable Force Text serialization in Unity before continuing.'
    }
    if ((Get-Content ProjectSettings/VersionControlSettings.asset -Raw) -notmatch 'm_Mode: Visible Meta Files') {
        throw 'Enable Visible Meta Files in Unity before continuing.'
    }
    $settings = [ordered]@{
        'merge.unityyamlmerge.name' = 'Unity Smart Merge'
        'merge.unityyamlmerge.driver' = ('"{0}" merge --fallback none -p --force "%O" "%B" "%A" "%A"' -f $executable)
        'mergetool.unityyamlmerge.cmd' = ('"{0}" merge --fallback none -p --force "$BASE" "$REMOTE" "$LOCAL" "$MERGED"' -f $executable)
        'mergetool.unityyamlmerge.trustExitCode' = 'true'
        'core.hooksPath' = '.githooks'
    }
    foreach ($key in $settings.Keys) {
        $value = $settings[$key]
        # Windows PowerShell's legacy native argument marshaller strips literal quotes.
        if ($PSVersionTable.PSVersion -lt [version]'7.3' -or $PSNativeCommandArgumentPassing -eq 'Legacy') {
            $value = $value.Replace('"', '\"')
        }
        & git config --local --replace-all $key $value
        if ($LASTEXITCODE -ne 0) { throw "Cannot configure $key" }
    }
    Write-Host "Unity $version merge driver and staged-meta hook configured locally."
}
finally { Pop-Location }
