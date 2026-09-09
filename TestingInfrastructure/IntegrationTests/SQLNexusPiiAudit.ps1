<#
.SYNOPSIS
Audits SQL Server user tables for values that remain sensitive after PiiScrubber processing.

.PARAMETER Server
SQL Server instance name. Defaults to localhost.

.PARAMETER Database
Database to audit. Defaults to sqlnexus.

.PARAMETER AssemblyPath
Path to the compiled SqlNexus.McpServer assembly containing PiiScrubber.

.PARAMETER ExcludeTable
One or more table names to skip. Accepts table or schema.table format.

.PARAMETER MaximumTableRows
Skips tables with more than this number of rows. A value of zero disables the limit.

.EXAMPLE
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1

.EXAMPLE
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1 -Server localhost -Database sqlnexus_michelle -ExcludeTable dbo.Counters,dbo.CounterData

.EXAMPLE
.\TestingInfrastructure\IntegrationTests\SQLNexusPiiAudit.ps1 -Database sqlnexus_Mat_ManagedTrc -MaximumTableRows 1000000
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Server = "localhost",

    [ValidateNotNullOrEmpty()]
    [string]$Database = "sqlnexus",

    [ValidateNotNullOrEmpty()]
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "..\..\SqlNexus.McpServer\bin\Release\SqlNexus.McpServer.exe"),

    [ValidateNotNullOrEmpty()]
    [string[]]$ExcludeTable = @(),

    [ValidateRange(0, [long]::MaxValue)]
    [long]$MaximumTableRows = 0
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
    throw "PII scrubber assembly not found: $AssemblyPath. Build SqlNexus.McpServer in Release or provide -AssemblyPath."
}

$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$scrubberType = $assembly.GetType("SqlNexus.McpServer.PiiScrubber", $true)
$scrubMethod = $scrubberType.GetMethod("Scrub", [Reflection.BindingFlags] "Public, Static")
$scrubDelegate = [Delegate]::CreateDelegate([Func[string, string]], $scrubMethod)

function Invoke-Scrub([string]$text) {
    return $scrubDelegate.Invoke($text)
}

function ConvertTo-JsonString([string]$value) {
    if ($null -eq $value) { return "" }
    return $value.Replace("\", "\\").Replace('"', '\"').Replace("`r", "\r").Replace("`n", "\n").Replace("`t", "\t")
}

function Get-Fingerprint([string]$value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($value)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "").Substring(0, 16)
    }
    finally {
        $sha.Dispose()
    }
}

function Get-Shape([string]$value) {
    $builder = New-Object Text.StringBuilder
    $limit = [Math]::Min($value.Length, 100)
    for ($index = 0; $index -lt $limit; $index++) {
        $character = $value[$index]
        if ([char]::IsLetter($character)) { [void]$builder.Append("A") }
        elseif ([char]::IsDigit($character)) { [void]$builder.Append("9") }
        elseif ([char]::IsWhiteSpace($character)) { [void]$builder.Append("_") }
        else { [void]$builder.Append($character) }
    }
    if ($value.Length -gt $limit) { [void]$builder.Append("...") }
    return $builder.ToString()
}

$detectors = [ordered]@{
    ResidualEmail = [regex]::new('\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b')
    ResidualIPv4 = [regex]::new('\b\d{1,3}(?:\.\d{1,3}){3}\b')
    ResidualGuid = [regex]::new('\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b')
    ResidualPhone = [regex]::new('\b(?:\+?\d{1,3}[\s.\-]?)?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}\b')
    ResidualDomainUserJsonEscaped = [regex]::new('\b[A-Za-z0-9_\-]{2,64}\\\\[A-Za-z0-9._\-]{2,64}\b')
    ResidualUncJsonEscaped = [regex]::new('\\{4}[A-Za-z0-9._\-]{2,}\\{2}[^\s"''<>,;]+')
    ResidualWindowsUserPathJsonEscaped = [regex]::new('[A-Za-z]:\\{2}(?:Users|Documents and Settings)\\{2}[^\\"''\s,;>]+', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    ResidualIPv6 = [regex]::new('(?<![0-9A-Fa-f:])(?:[0-9A-Fa-f]{1,4}:){2,7}[0-9A-Fa-f]{0,4}(?![0-9A-Fa-f:])')
    ResidualMacAddress = [regex]::new('\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b')
    ResidualCredentialAssignment = [regex]::new('(?:password|pwd|accountkey|accesskey|secret|token)\s*[=:]\s*[^;\s,"}]{4,}', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    ResidualLocalUpn = [regex]::new('\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9_\-]+\b')
}

$sensitiveColumnPattern = [regex]::new('(?:login|user(?:name)?|host(?:name)?|machine|computer|server(?:name)?|client_net_address|local_net_address|email|phone|owner|principal|path)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
$findings = @{}
$tablesScanned = 0
$rowsScanned = [long]0
$candidateCellsScanned = [long]0
$changedCells = [long]0
$tableErrors = New-Object Collections.Generic.List[object]
$largeTablesSkipped = New-Object Collections.Generic.List[object]
$excludedTableSet = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($excludedTable in $ExcludeTable) {
    [void]$excludedTableSet.Add($excludedTable.Trim())
}

function Add-Finding([string]$category, [string]$schemaName, [string]$tableName, [string]$columnName, [string]$value) {
    $key = "$category|$schemaName|$tableName|$columnName"
    if (-not $findings.ContainsKey($key)) {
        $findings[$key] = [ordered]@{
            Category = $category
            Schema = $schemaName
            Table = $tableName
            Column = $columnName
            Count = 0
            SampleFingerprint = Get-Fingerprint $value
            SampleLength = $value.Length
            SampleShape = Get-Shape $value
        }
    }
    $findings[$key].Count++
}

$connectionStringBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connectionStringBuilder["Data Source"] = $Server
$connectionStringBuilder["Initial Catalog"] = $Database
$connectionStringBuilder["Integrated Security"] = $true
$connectionStringBuilder["Application Name"] = "SqlNexus PII Audit"
$connectionStringBuilder["Encrypt"] = $false
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionStringBuilder.ConnectionString
$connection.Open()
try {
    $metadataCommand = $connection.CreateCommand()
    $metadataCommand.CommandText = @"
WITH row_counts AS
(
    SELECT object_id, SUM(rows) AS row_count
    FROM sys.partitions
    WHERE index_id IN (0, 1)
    GROUP BY object_id
),
candidate_columns AS
(
    SELECT c.object_id, c.column_id, c.name AS column_name, ty.name AS type_name
    FROM sys.columns AS c
    JOIN sys.types AS ty
      ON ty.system_type_id = c.system_type_id
     AND ty.user_type_id = ty.system_type_id
    WHERE ty.name IN
    (
        'char', 'varchar', 'nchar', 'nvarchar', 'text', 'ntext', 'xml',
        'uniqueidentifier', 'binary', 'varbinary', 'image'
    )
)
SELECT
    s.name AS schema_name,
    t.name AS table_name,
    ISNULL(rc.row_count, 0) AS row_count,
    cc.column_name,
    cc.type_name
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN row_counts AS rc ON rc.object_id = t.object_id
LEFT JOIN candidate_columns AS cc ON cc.object_id = t.object_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, cc.column_id;
"@
    $metadataReader = $metadataCommand.ExecuteReader()
    $tables = New-Object Collections.Generic.List[object]
    $tablesByName = @{}
    while ($metadataReader.Read()) {
        $schemaName = $metadataReader.GetString(0)
        $tableName = $metadataReader.GetString(1)
        $tableKey = $schemaName + [char]31 + $tableName
        if (-not $tablesByName.ContainsKey($tableKey)) {
            $tableInfo = [pscustomobject]@{
                Schema = $schemaName
                Table = $tableName
                RowCount = $metadataReader.GetInt64(2)
                Columns = New-Object Collections.Generic.List[string]
            }
            $tablesByName[$tableKey] = $tableInfo
            $tables.Add($tableInfo)
        }

        if (-not $metadataReader.IsDBNull(3)) {
            $columnName = $metadataReader.GetString(3)
            $typeName = $metadataReader.GetString(4)
            $isBinary = $typeName -in @('binary', 'varbinary', 'image')
            if (-not $isBinary -or $sensitiveColumnPattern.IsMatch($columnName)) {
                $tablesByName[$tableKey].Columns.Add($columnName)
            }
        }
    }
    $metadataReader.Close()

    foreach ($table in $tables) {
        $qualifiedTableName = $table.Schema + "." + $table.Table
        if ($excludedTableSet.Contains($table.Table) -or $excludedTableSet.Contains($qualifiedTableName)) {
            Write-Verbose "Skipping excluded table $qualifiedTableName"
            continue
        }

        if ($MaximumTableRows -gt 0 -and $table.RowCount -gt $MaximumTableRows) {
            Write-Verbose ("Skipping large table {0}: {1:N0} rows" -f $qualifiedTableName, $table.RowCount)
            $largeTablesSkipped.Add([pscustomobject]@{
                Schema = $table.Schema
                Table = $table.Table
                RowCount = $table.RowCount
            })
            continue
        }

        $tablesScanned++
        $rowsScanned += $table.RowCount
        if ($table.Columns.Count -eq 0 -or $table.RowCount -eq 0) { continue }

        Write-Verbose ("Scanning {0}.{1}: {2:N0} rows, {3} candidate columns" -f
            $table.Schema, $table.Table, $table.RowCount, $table.Columns.Count)

        $quotedSchema = "[" + $table.Schema.Replace("]", "]]" ) + "]"
        $quotedTable = "[" + $table.Table.Replace("]", "]]" ) + "]"
        $quotedColumns = $table.Columns | ForEach-Object { "[" + $_.Replace("]", "]]" ) + "]" }
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 0
        $command.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SELECT $($quotedColumns -join ',') FROM $quotedSchema.$quotedTable;"
        $reader = $null
        try {
            $reader = $command.ExecuteReader([Data.CommandBehavior]::SequentialAccess)
            while ($reader.Read()) {
                for ($ordinal = 0; $ordinal -lt $reader.FieldCount; $ordinal++) {
                    if ($reader.IsDBNull($ordinal)) { continue }
                    $columnName = $reader.GetName($ordinal)
                    $candidateCellsScanned++
                    $rawValue = $reader.GetValue($ordinal)
                    if ($rawValue -is [byte[]]) { $value = [Convert]::ToBase64String($rawValue) }
                    else { $value = [Convert]::ToString($rawValue, [Globalization.CultureInfo]::InvariantCulture) }
                    if ([string]::IsNullOrWhiteSpace($value)) { continue }

                    $serialized = '"' + (ConvertTo-JsonString $columnName) + '": "' + (ConvertTo-JsonString $value) + '"'
                    $scrubbed = Invoke-Scrub $serialized
                    $changed = -not [string]::Equals($serialized, $scrubbed, [StringComparison]::Ordinal)
                    if ($changed) { $changedCells++ }

                    foreach ($detector in $detectors.GetEnumerator()) {
                        if ($detector.Value.IsMatch($scrubbed)) {
                            Add-Finding $detector.Key $table.Schema $table.Table $columnName $scrubbed
                        }
                    }

                    if (-not $changed -and $sensitiveColumnPattern.IsMatch($columnName) -and $value -match '[A-Za-z]' -and $value -notmatch '^<[^>]+>$') {
                        Add-Finding "SensitiveColumnValueUnchanged" $table.Schema $table.Table $columnName $serialized
                    }
                }
            }
            $reader.Close()
        }
        catch {
            if ($null -ne $reader -and -not $reader.IsClosed) { $reader.Close() }
            $tableErrors.Add([pscustomobject]@{
                Schema = $table.Schema
                Table = $table.Table
                ErrorType = $_.Exception.GetType().FullName
                Message = $_.Exception.Message
            })
        }
    }
}
finally {
    $connection.Close()
}

$result = [ordered]@{
    Server = $Server
    Database = $Database
    TablesScanned = $tablesScanned
    RowsScanned = $rowsScanned
    PiiCandidateCellsScanned = $candidateCellsScanned
    CellsChangedByScrubber = $changedCells
    ExcludedTables = [string[]]$ExcludeTable
    MaximumTableRows = $MaximumTableRows
    LargeTablesSkipped = $largeTablesSkipped.ToArray()
    TableErrors = $tableErrors.ToArray()
    Findings = [object[]]($findings.Values | Sort-Object Category, Schema, Table, Column)
}
$result | ConvertTo-Json -Depth 6