$ErrorActionPreference = 'Continue'
try {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

    # The protected files live in the repository root (not next to this project). Walk up from the
    # project folder until we find the folder that contains both AI\Skills and .github\agents.
    $repoRoot = $scriptDir
    while ($repoRoot -and -not ((Test-Path (Join-Path $repoRoot "AI\Skills")) -and (Test-Path (Join-Path $repoRoot ".github\agents")))) {
        $parent = Split-Path -Parent $repoRoot
        if ($parent -eq $repoRoot) { $repoRoot = $null; break }
        $repoRoot = $parent
    }

    if (-not $repoRoot) {
        Write-Output "GenerateFileHashes.ps1: could not locate repository root (AI\Skills + .github\agents)."
        exit 0
    }

    # Collect the protected files: the diagnostic agent definition first, then all skill files.
    $files = @()
    $agentFile = Join-Path $repoRoot ".github\agents\sql-nexus-diagnostic.agent.md"
    if (Test-Path $agentFile) { $files += Get-Item $agentFile }
    $files += Get-ChildItem (Join-Path $repoRoot "AI\Skills\*.md") -File | Sort-Object Name

    Write-Output "Paste the following into FileIntegrity.cs:"
    Write-Output "`tprivate static readonly Dictionary<string, string> ProtectedFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {"
    foreach ($file in $files) {
        $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToUpper()
        # Emit the repository-relative path with forward slashes to match the dictionary keys.
        $relative = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        Write-Output "`t`t{ `"$relative`", `"$hash`" },"
    }
    Write-Output "`t};"
}
catch {
    # This script only prints advisory hash output; never fail the build because of it.
    Write-Output "GenerateFileHashes.ps1: skipped due to error: $($_.Exception.Message)"
}

# Always succeed so a transient scripting error can never break the build.
exit 0

