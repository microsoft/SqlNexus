# GenerateSqlHashes.ps1
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFiles = Get-ChildItem "$scriptDir\*.sql" -File
Write-Output "Paste the following into ScriptIntegrity.cs:"
Write-Output "`tprivate static readonly Dictionary<string, string> ScriptHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {"
foreach ($file in $sqlFiles) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToUpper()
    Write-Output "`t`t{ `"$($file.Name)`", `"$hash`" },"
}
Write-Output "`t};"