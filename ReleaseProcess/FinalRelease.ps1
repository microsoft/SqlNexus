param (
    [string]$ReleasePath
)



# Check if the script is being run with the correct number of parameters
if ($PSBoundParameters.Count -ne 1) {
    Write-Host "Usage: .\FinalRelease.ps1 -InitialPath <path_to_root_folder>" -ForegroundColor Red
    exit 1
}

# Check if the current user is an administrator
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "The script must be run as an administrator. Please restart PowerShell with elevated permissions."
    exit 1
}

# Validate the destination root folder
if (-not (Test-Path -Path $ReleasePath)) {
    Write-Host "The provided initial path does not exist: '$ReleasePath'" -ForegroundColor Red
    exit 1
}

$SignedFilesToCopy = @(
    "\SQLNexus\Bin\Release\BulkLoadEx.dll",
    "\SQLNexus\Bin\Release\LinuxPerfImporter.dll",
    "\SQLNexus\Bin\Release\NexusInterfaces.dll",
    "\SQLNexus\Bin\Release\PerfmonImporter.dll",
    "\SQLNexus\Bin\Release\ReadTraceNexusImporter.dll",
    "\SQLNexus\Bin\Release\RowsetImportEngine.dll",
    "\SQLNexus\Bin\Release\sqlnexus.exe"
)

# Define the paths to the folders
$filesToSignFolder = Join-Path -Path $ReleasePath -ChildPath "FilesToSign"
$signedFilesFolder = Join-Path -Path $ReleasePath -ChildPath "SignedFiles"
$releaseSignedPrepFolder = Join-Path -Path $ReleasePath -ChildPath "Release_Signed_Prep"
$releaseSignedFolder = Join-Path -Path $ReleasePath -ChildPath "Release_Signed"
$releaseUnsignedFolder = Join-Path -Path $ReleasePath -ChildPath "Release_Unsigned"

Write-Host "Release Signed Prep Folder: $releaseSignedPrepFolder"

# Validate the existence of all three folders in a single check
if (-not (Test-Path -Path $filesToSignFolder) -or 
    -not (Test-Path -Path $signedFilesFolder) -or 
    -not (Test-Path -Path $releaseSignedPrepFolder)) {
    Write-Host "One or more required folders do not exist. Please ensure that '\FilesToSign', '\SignedFiles', and '\Release_Signed_Prep' folders are present." -ForegroundColor Red
    exit 1
}

# Get the list of files in each folder
$filesToSign = Get-ChildItem -Path $filesToSignFolder -File
$signedFiles = Get-ChildItem -Path $signedFilesFolder -File


# Compare the number of files
if (($filesToSign.Count) -ne ($signedFiles.Count)) {
    Write-Host "The number of files in '\FilesToSign' ($($filesToSign.Count)) and '\SignedFiles' ($($signedFiles.Count))) folders do not match." -ForegroundColor Red
}
else {
    Write-Host "The number of files in '\FilesToSign' ($($filesToSign.Count)) and '\SignedFiles' ($($signedFiles.Count)) folders match."
}

# Compare the signed files and filestosign filenames
$filesToSignNames = $filesToSign.Name
$signedFilesNames = $signedFiles.Name
$missingFiles = $filesToSignNames | Where-Object { $_ -notin $signedFilesNames }
if ($missingFiles.Count -gt 0) {
    Write-Host "The following file(s) are missing in the '\SignedFiles' folder: `r`n  $($missingFiles -join "`r`n  ")" -ForegroundColor Red
    exit 1
}

# Validate that the files in SignedFiles are digitally signed
$invalidCount = 0
foreach ($file in $signedFiles) {
    $signature = Get-AuthenticodeSignature $file.FullName
    if ($signature.Status -ne 'Valid') {
        Write-Host "The file '$($file.FullName)' is not properly signed. Status: $($signature.StatusMessage)" -ForegroundColor Red
        $invalidCount++
    }
}

# Check if any files were found to be invalidly signed and exit with an error if so
if ($invalidCount -gt 0) {
    Write-Host "$invalidCount file(s) in the '\SignedFiles' folder are not properly signed. Exiting."
    exit 1
}
else {
    Write-Host "All files in the '\SignedFiles' folder are properly signed." -ForegroundColor Green
}

# Validate that there are no extra files in Release_Signed_Prep that are not in SignedFilesToCopy and vice versa

# Get files in Release_Signed_Prep folder and filter to files that are to be overwritten with signed counterparts
# Extract just the filenames from SignedFilesToCopy
$SignedFileNamesToCopy = $SignedFilesToCopy | ForEach-Object { Split-Path -Path $_ -Leaf }

# Use the extracted filenames in the Where-Object filter
$releaseSignedPrepFiles = Get-ChildItem -Path $releaseSignedPrepFolder -Recurse -File | Where-Object { $_.Name -in $SignedFileNamesToCopy }


# Check if there are any files in Release_Signed_Prep folder
if ($releaseSignedPrepFiles.Count -eq 0) {
    Write-Host "No files found in the '\Release_Signed_Prep' folder. Please ensure that the folder contains the expected signed files." -ForegroundColor Red
    exit 1
}

# Get the full file names from  Release_Signed_Prep folder and remove the folder path to get just the relative file names for comparison
$releaseSignedPrepFileNames = $releaseSignedPrepFiles.FullName | ForEach-Object { $_.Substring($releaseSignedPrepFolder.Length) }

$releaseSignedPrepFileNames

# Check for files in Release_Signed_Prep that are not in SignedFilesToCopy
$missingSignedFilesToCopy = $releaseSignedPrepFileNames | Where-Object { $_ -notin $SignedFilesToCopy }

# Check for files in SignedFilesToCopy array that are not in Release_Signed_Prep
$missingReleaseSignedPrepFiles =  $SignedFilesToCopy | Where-Object {$_ -notin $releaseSignedPrepFileNames}

# If there are any missing files in either direction, report them and exit with an error
# This ensures that the files in SignedFilesToCopy are present in Release_Signed_Prep and vice versa

if ($missingSignedFilesToCopy.Count -gt 0 -or $missingReleaseSignedPrepFiles.Count -gt 0) {
    if ($missingSignedFilesToCopy.Count -gt 0) {
        Write-Host "Files missing in SignedFilesToCopy array:`r`n  $($missingSignedFilesToCopy -join "`r`n  ")" -ForegroundColor Red
    }
    if ($missingReleaseSignedPrepFiles.Count -gt 0) {
        Write-Host "Files missing in Release_Signed_Prep directory:`r`n  $($missingReleaseSignedPrepFiles -join "`r`n  ")" -ForegroundColor Red
    }
    exit 1
}

# Validate that each file in SignedFilesToCopy exists in Release_Signed_Prep with matching subdirectory structure
$copiedFilesCount = 0

foreach ($file in $SignedFilesToCopy) {
    # Construct the full path to the source file in the SignedFiles folder. 
    # They are expected to be in the one flat folder structure, so we just need to join the signedFilesFolder with the file name only.
    $sourceFile = Join-Path -Path $signedFilesFolder -ChildPath $file.Split("\")[-1]

    # Check if it exists in the Release_Signed_Prep folder with the subdirectory structure coming from $SignedFilesToCopy
    # Use TrimStart to remove the leading backslash from the file path to avoid issues with Join-Path

    if (-not (Test-Path -Path (Join-Path -Path $releaseSignedPrepFolder -ChildPath $file.TrimStart("\")))) 
    {
        # If the file does not exist in the Release_Signed_Prep folder, report an error and exit
        Write-Host "The file '$sourceFile' does not exist in the '\Release_Signed_Prep' folder with matching subdirectory structure." -ForegroundColor Red
        exit 1
    } 
    else 
    {
        # Copy the file to the Release_Signed_Prep folder, preserving the subdirectory structure
        $destinationFile = Join-Path -Path $releaseSignedPrepFolder -ChildPath $file.TrimStart("\")
        Copy-Item -Path $sourceFile -Destination $destinationFile -Force -ErrorAction Stop

        $copiedFilesCount++
        # Output the copied file information
        Write-Host "Copied '$sourceFile' to '$destinationFile'"
    }
}

# Output the total number of copied files and expected to copy files
Write-Host "Total of $($SignedFilesToCopy.Count) file(s) expected to be copied from SignedFilesToCopy array"
Write-Host "Total of $copiedFilesCount file(s) actually copied to '$releaseSignedPrepFolder' "



# Final step: Rename the Release_Signed_Prep folder to Release_Signed
if (Test-Path -Path $releaseSignedFolder) {
    Write-Host "The destination folder '$releaseSignedFolder' already exists. Please remove or rename it before running the script." -ForegroundColor Red
    exit 1
}
try {
    $seconds_to_wait = 15
    Write-Host "Waiting $seconds_to_wait seconds for files to be processed by Anti-Virus and OneDrive sync..."
    Start-Sleep -Seconds $seconds_to_wait
    Rename-Item -Path $releaseSignedPrepFolder -NewName $releaseSignedFolder -ErrorAction Stop
} catch {
    Write-Host "Failed to rename '$releaseSignedPrepFolder' to '$releaseSignedFolder'. Error: $_" -ForegroundColor Red
}

#check if the folder rename was successful
if (Test-Path -Path $releaseSignedFolder) {
    Write-Host "The folder '$releaseSignedFolder' was successfully renamed from '$releaseSignedPrepFolder' and is ready for release."
} else {
    Write-Host "The folder '$releaseSignedFolder' was not found after renaming. Please check the rename operation." -ForegroundColor Red
}
# End of script