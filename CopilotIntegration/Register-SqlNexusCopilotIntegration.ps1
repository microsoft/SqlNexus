[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Server = "localhost",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Database = "SqlNexus",

    [Parameter()]
    [string] $Database2 = "",

    [Parameter()]
    [ValidateSet("Block", "ImpersonateReader")]
    [string] $ElevatedPrincipalPolicy = "Block",

    # Optional: the client does not require encryption (the connection is still encrypted if SQL Server
    # requires it, e.g. Force Encryption). Mandatory: encryption required (works with Force Encryption).
    # Strict: TDS 8.0 encryption; required when SQL Server uses Force Strict Encryption.
    [Parameter(Mandatory = $true)]
    [ValidateSet("Optional", "Mandatory", "Strict")]
    [string] $EncryptionMode,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Validate", "TrustServerCertificate")]
    [string] $CertificateValidation,

    # Defaulted after the param block: in Windows PowerShell 5.1, $PSScriptRoot is empty inside a
    # parameter default when the script also declares a Mandatory parameter.
    [Parameter()]
    [string] $InstallRoot,

    [Parameter()]
    [string] $McpExecutablePath,

    [Parameter()]
    [string] $CopilotHome = (Join-Path $HOME ".copilot"),

    [Parameter()]
    [string] $VsCodeUserData = (Join-Path $env:APPDATA "Code\User"),

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $McpOnly,

    # Windows (default): Windows Integrated Authentication, no secret involved.
    # Sql: SQL authentication. The password is never written to a configuration file or passed on a
    # command line; it is collected securely and stored in a DPAPI-protected file for the current user.
    [Parameter()]
    [ValidateSet("Windows", "Sql")]
    [string] $AuthMode = "Windows",

    [Parameter()]
    [string] $UserId,

    # Optional, for automation. When omitted, the script prompts for the password (masked).
    [Parameter()]
    [System.Security.SecureString] $SqlPassword,

    # Replace an existing stored credential. By default an existing credential is kept.
    [Parameter()]
    [switch] $UpdateCredential
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Split-Path -Parent $PSScriptRoot
}
$serverName = "sqlnexus_mcp"
# Most callers rely on the staged/release layout convention (InstallRoot-based). Callers that
# already know the correct executable path for their layout -- e.g. Test-McpServer.ps1, which
# resolves both the dev (bin\Release) and staged/release layouts -- can override it directly so
# that layout-detection logic lives in exactly one place.
$mcpExecutable = if (-not [string]::IsNullOrWhiteSpace($McpExecutablePath)) {
    $McpExecutablePath
} else {
    Join-Path $InstallRoot "SqlNexus.McpServer\SqlNexus.McpServer.exe"
}
$agentSource = Join-Path $InstallRoot ".github\agents\sql-nexus-diagnostic.agent.md"
$skillsDirectory = Join-Path $InstallRoot "AI\Skills"
$copilotConfigPath = Join-Path $CopilotHome "mcp-config.json"
$vscodeConfigPath = Join-Path $VsCodeUserData "mcp.json"
$agentDestination = Join-Path $CopilotHome "agents\sql-nexus-diagnostic.agent.md"

function Write-Status {
    param(
        [ValidateSet("INFO", "WARNING", "ERROR")]
        [string] $Level,
        [string] $Message
    )

    $color = switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } default { "Cyan" } }
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message" -ForegroundColor $color
}

trap {
    Write-Host ""
    Write-Status -Level "ERROR" -Message "SQL Nexus Copilot integration was not registered."
    Write-Status -Level "ERROR" -Message $_.Exception.Message
    exit 1
}

if ($AuthMode -eq "Sql") {
    if ([string]::IsNullOrWhiteSpace($UserId)) {
        throw "-UserId is required when -AuthMode is Sql."
    }
    $UserId = $UserId.Trim()
}
elseif ($PSBoundParameters.ContainsKey("UserId") -or $PSBoundParameters.ContainsKey("SqlPassword") -or $UpdateCredential) {
    throw "-UserId, -SqlPassword and -UpdateCredential are only valid with -AuthMode Sql."
}

function Read-JsonConfiguration {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{}
    }

    $content = [System.IO.File]::ReadAllText($Path)
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [pscustomobject]@{}
    }

    try {
        $configuration = $content | ConvertFrom-Json
    }
    catch {
        throw "Cannot register SQL Nexus because '$Path' does not contain valid JSON. $($_.Exception.Message)"
    }

    if ($null -eq $configuration -or $configuration -is [System.Array]) {
        throw "Cannot register SQL Nexus because '$Path' must contain a JSON object."
    }

    return $configuration
}

function Get-OrAddObjectProperty {
    param(
        [object] $Object,
        [string] $Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $value = [pscustomobject]@{}
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $value
        return $value
    }

    if ($null -eq $property.Value -or $property.Value -is [System.Array] -or $property.Value -isnot [psobject]) {
        throw "Cannot register SQL Nexus because the '$Name' property must be a JSON object."
    }

    return $property.Value
}

function Set-ServerEntry {
    param(
        [object] $Container,
        [string] $Name,
        [object] $Entry
    )

    $property = $Container.PSObject.Properties[$Name]
    if ($null -ne $property) {
        $existingJson = $property.Value | ConvertTo-Json -Depth 20 -Compress
        $expectedJson = $Entry | ConvertTo-Json -Depth 20 -Compress
        if (-not $Force) {
            if ($existingJson -ne $expectedJson) {
                throw "An MCP server named '$Name' is already configured differently. Re-run with -Force to replace only that entry."
            }

            return $false
        }

        $property.Value = $Entry
        return $true
    }

    $Container | Add-Member -MemberType NoteProperty -Name $Name -Value $Entry
    return $true
}

function Write-JsonConfiguration {
    param(
        [string] $Path,
        [object] $Configuration
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $temporaryPath = "$Path.$PID.tmp"
    $json = ($Configuration | ConvertTo-Json -Depth 20) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($temporaryPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function ConvertTo-ProcessArgument {
    param([string] $Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

# Runs the MCP server executable in one of its credential modes. The password, when needed, is
# written to the process's standard input as UTF-8 bytes: never a command-line argument or an
# environment variable, both of which other processes can read.
function Invoke-McpCredentialCommand {
    param(
        [string[]] $Arguments,
        [byte[]] $StandardInputBytes
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $mcpExecutable
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " ")
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.RedirectStandardInput = ($null -ne $StandardInputBytes)

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if ($null -ne $StandardInputBytes) {
            $inputStream = $process.StandardInput.BaseStream
            $inputStream.Write($StandardInputBytes, 0, $StandardInputBytes.Length)
            $inputStream.Flush()
            $process.StandardInput.Close()
        }

        $output = $process.StandardOutput.ReadToEnd()
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output; Error = $errorText }
    }
    finally {
        $process.Dispose()
    }
}

# Copies a SecureString into a UTF-8 byte array without creating a managed string copy.
function ConvertFrom-SecureStringToUtf8Bytes {
    param([System.Security.SecureString] $SecureString)

    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    $chars = New-Object char[] $SecureString.Length
    try {
        for ($i = 0; $i -lt $chars.Length; $i++) {
            $chars[$i] = [char][System.Runtime.InteropServices.Marshal]::ReadInt16($bstr, 2 * $i)
        }

        return ,([System.Text.Encoding]::UTF8.GetBytes($chars))
    }
    finally {
        [System.Array]::Clear($chars, 0, $chars.Length)
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Read-SqlPasswordSecurely {
    try {
        $first = Read-Host -Prompt "Enter the SQL password for '$UserId' on '$Server'" -AsSecureString
        $second = Read-Host -Prompt "Confirm the SQL password" -AsSecureString
    }
    catch {
        throw "The SQL password could not be requested because this session is not interactive. Re-run in an interactive PowerShell window, or pass -SqlPassword (a SecureString)."
    }

    $firstBytes = ConvertFrom-SecureStringToUtf8Bytes -SecureString $first
    $secondBytes = ConvertFrom-SecureStringToUtf8Bytes -SecureString $second
    try {
        $same = ($firstBytes.Length -eq $secondBytes.Length)
        for ($i = 0; $same -and $i -lt $firstBytes.Length; $i++) {
            if ($firstBytes[$i] -ne $secondBytes[$i]) { $same = $false }
        }
    }
    finally {
        [System.Array]::Clear($firstBytes, 0, $firstBytes.Length)
        [System.Array]::Clear($secondBytes, 0, $secondBytes.Length)
    }

    if (-not $same) {
        throw "The passwords did not match. No credential was stored."
    }

    return $first
}

function Initialize-SqlCredential {
    $check = Invoke-McpCredentialCommand -Arguments @("--check-credential", "--server", $Server, "--user-id", $UserId)
    if ($check.ExitCode -eq 0 -and -not $UpdateCredential) {
        Write-Status -Level "INFO" -Message "A stored SQL credential already exists for user '$UserId' on '$Server' and was kept. If this login's password has changed in SQL Server, use -UpdateCredential to update the locally stored credential."
        if ($null -ne $SqlPassword) {
            Write-Status -Level "WARNING" -Message "-SqlPassword was ignored because a stored credential already exists. Add -UpdateCredential to update the locally stored credential."
        }
        return
    }

    $securePassword = $SqlPassword
    if ($null -eq $securePassword) {
        $securePassword = Read-SqlPasswordSecurely
    }
    if ($securePassword.Length -eq 0) {
        throw "The SQL password must not be empty. No credential was stored."
    }

    $passwordBytes = ConvertFrom-SecureStringToUtf8Bytes -SecureString $securePassword
    $inputBytes = New-Object byte[] ($passwordBytes.Length + 1)
    [System.Array]::Copy($passwordBytes, $inputBytes, $passwordBytes.Length)
    $inputBytes[$passwordBytes.Length] = 10
    try {
        $result = Invoke-McpCredentialCommand `
            -Arguments @("--set-credential", "--server", $Server, "--user-id", $UserId, "--password-stdin") `
            -StandardInputBytes $inputBytes
    }
    finally {
        [System.Array]::Clear($passwordBytes, 0, $passwordBytes.Length)
        [System.Array]::Clear($inputBytes, 0, $inputBytes.Length)
    }

    if ($result.ExitCode -ne 0) {
        throw "Storing the SQL credential failed. $($result.Error.Trim())"
    }

    Write-Status -Level "INFO" -Message "Stored the SQL credential for user '$UserId' on '$Server' (protected for the current Windows user)."
}

$requiredPaths = @($mcpExecutable)
if (-not $McpOnly) {
    $requiredPaths += @($agentSource, $skillsDirectory)
}

foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "The SQL Nexus installation is incomplete. Required path not found: $requiredPath"
    }
}

$arguments = @("--server", $Server, "--database", $Database)
if (-not [string]::IsNullOrWhiteSpace($Database2)) {
    $arguments += @("--database2", $Database2)
}
if ($AuthMode -eq "Sql") {
    $arguments += @("--trusted-connection", "false", "--user-id", $UserId)
}
else {
    $arguments += @("--trusted-connection", "true")
}
# Reject unsafe combinations before anything is written; the MCP server enforces the same rules.
if ($CertificateValidation -eq "TrustServerCertificate" -and $EncryptionMode -eq "Strict") {
    throw "-EncryptionMode Strict always requires certificate validation (-CertificateValidation Validate) and cannot be combined with -CertificateValidation TrustServerCertificate. Use -EncryptionMode Mandatory for a lab server with an untrusted certificate."
}
if ($CertificateValidation -eq "TrustServerCertificate" -and $EncryptionMode -eq "Optional") {
    throw "-EncryptionMode Optional cannot be combined with -CertificateValidation TrustServerCertificate. Use -EncryptionMode Mandatory for an encrypted connection to a lab server with an untrusted certificate."
}
$arguments += @("--encrypt", $EncryptionMode)
if ($CertificateValidation -eq "TrustServerCertificate") {
    # The MCP server refuses --trust-server-certificate unless the separately named bypass opt-in is also present.
    $arguments += @("--trust-server-certificate", "true", "--allow-certificate-validation-bypass", "true")
}
else {
    $arguments += @("--trust-server-certificate", "false")
}
$policyArgument = if ($ElevatedPrincipalPolicy -eq "ImpersonateReader") { "impersonate-reader" } else { "block" }
$arguments += @("--elevated-principal-policy", $policyArgument)
$vscodeEntry = [pscustomobject][ordered]@{
    type = "stdio"
    command = $mcpExecutable
    args = $arguments
}
$copilotEntry = [pscustomobject][ordered]@{
    type = "stdio"
    command = $mcpExecutable
    args = $arguments
    env = [pscustomobject]@{}
    tools = @("*")
}

$vscodeConfiguration = Read-JsonConfiguration -Path $vscodeConfigPath
$copilotConfiguration = Read-JsonConfiguration -Path $copilotConfigPath
$vscodeServers = Get-OrAddObjectProperty -Object $vscodeConfiguration -Name "servers"
$copilotServers = Get-OrAddObjectProperty -Object $copilotConfiguration -Name "mcpServers"
$vscodeChanged = Set-ServerEntry -Container $vscodeServers -Name $serverName -Entry $vscodeEntry
$copilotChanged = Set-ServerEntry -Container $copilotServers -Name $serverName -Entry $copilotEntry

$agentChanged = $false
if (-not $McpOnly) {
    $agentContent = [System.IO.File]::ReadAllText($agentSource)
    $portableSkillsPath = $skillsDirectory.Replace('\', '/').TrimEnd('/') + "/"
    $agentContent = $agentContent.Replace("AI/Skills/", $portableSkillsPath)
    $agentChanged = $true
    if ((Test-Path -LiteralPath $agentDestination -PathType Leaf) -and -not $Force) {
        $existingAgentContent = [System.IO.File]::ReadAllText($agentDestination)
        if ($existingAgentContent -ne $agentContent) {
            throw "A different SQL Nexus diagnostic agent already exists at '$agentDestination'. Re-run with -Force to replace it."
        }

        $agentChanged = $false
    }
}

if ($AuthMode -eq "Sql") {
    # Runs after every validation/conflict check above so a rejected registration never leaves a credential behind.
    Initialize-SqlCredential
}

if ($vscodeChanged) {
    Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
}
if ($copilotChanged) {
    Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration
}

if ($agentChanged) {
    $agentDirectory = Split-Path -Parent $agentDestination
    if (-not (Test-Path -LiteralPath $agentDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($agentDestination, $agentContent, (New-Object System.Text.UTF8Encoding($false)))
}

if ($McpOnly -and -not $vscodeChanged -and -not $copilotChanged) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server is already registered. Existing matching settings were not replaced."
}
elseif ($McpOnly) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server registered. The custom agent was not changed."
}
elseif (-not $vscodeChanged -and -not $copilotChanged -and -not $agentChanged) {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration is already registered. Existing matching settings were not replaced."
}
else {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration registered."
}
Write-Status -Level "INFO" -Message "MCP executable: $mcpExecutable"
Write-Status -Level "INFO" -Message "SQL Server: $Server"
Write-Status -Level "INFO" -Message "Database: $Database"
if (-not [string]::IsNullOrWhiteSpace($Database2)) {
    Write-Status -Level "INFO" -Message "Comparison database (--database2): $Database2"
}
if ($AuthMode -eq "Sql") {
    Write-Status -Level "INFO" -Message "Authentication: SQL Authentication (user '$UserId'); the password is stored in a DPAPI-protected file for the current Windows user"
    Write-Status -Level "WARNING" -Message "The stored SQL credential works only in the context of this Windows user on this machine and is keyed by the exact server name '$Server'. If this login's password changes in SQL Server, re-run this script with -UpdateCredential to update the locally stored credential."
}
else {
    Write-Status -Level "INFO" -Message "Authentication: Windows Integrated Authentication"
}
Write-Status -Level "INFO" -Message "Encryption mode: $EncryptionMode"
Write-Status -Level "INFO" -Message "Certificate validation: $CertificateValidation"
if ($EncryptionMode -eq "Optional") {
    Write-Status -Level "WARNING" -Message "Encryption mode Optional means encryption is not required by the client. The connection is still encrypted if SQL Server requires encryption, but otherwise it can be observed or tampered with on the network."
}
if ($CertificateValidation -eq "TrustServerCertificate") {
    Write-Status -Level "WARNING" -Message "SQL Server certificate validation is disabled. An on-path attacker could impersonate the SQL Server. Use only in a lab environment."
}
Write-Status -Level "INFO" -Message "Elevated principal policy: $ElevatedPrincipalPolicy"
if ($ElevatedPrincipalPolicy -eq "ImpersonateReader") {
    Write-Status -Level "INFO" -Message "Elevated identities are accepted only to enter the non-revertible SqlNexusMcpReader context; MCP queries do not run elevated."
}
Write-Status -Level "INFO" -Message "VS Code MCP configuration: $vscodeConfigPath"
Write-Status -Level "INFO" -Message "Copilot CLI MCP configuration: $copilotConfigPath"
if (-not $McpOnly) {
    Write-Status -Level "INFO" -Message "Custom agent: $agentDestination"
}