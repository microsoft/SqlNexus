param (
    [string]$DestinationRootFolder
)


try {
        
    # Define the source binary files to be signed
    $SourceFiles = @(
        "BulkLoadEx.dll",
        "LinuxPerfImporter.dll",
        "NexusInterfaces.dll",
        "PerfmonImporter.dll",
        "ReadTraceNexusImporter.dll",
        "RowsetImportEngine.dll",
        "sqlnexus.exe"
    )

    # Define an exclusion list for files and folders that should not be included in the project directory
    $ExcludeFilesUnsignedArray = @(
        "PrepareFilesForSigning.ps1", 
        "FinalRelease.ps1",
        "azuredevops-pipelines.yml",
        ".gitignore"
      
    )

    # Define an exclusion list for the Signed_Prep folder, which includes all files in $ExcludeFilesUnsigned and additional files
$ExcludeFilesSignedArray = $ExcludeFilesUnsignedArray + @(
        "BulkLoadEx.pdb",
        "LinuxPerfImporter.pdb",
        "Microsoft.Data.SqlClient.SNI.arm64.pdb",
        "Microsoft.Data.SqlClient.SNI.x64.pdb",
        "Microsoft.Data.SqlClient.SNI.x86.pdb",
        "NexusInterfaces.pdb",
        "PerfmonImporter.pdb",
        "ReadTraceNexusImporter.pdb",
        "RowsetImportEngine.pdb",
        "sqlnexus.pdb"
    )
    
    # exclude these folders from the project directory
    $ExcludeFoldersUnsignedArr = @(
        ".vscode"
    )

    $ExcludeFoldersSignedArr = $ExcludeFoldersUnsignedArr + @(
        "app.publish"
    )
    # Go to the root solution directory 
    $Nexus_RootDir = (Get-Item (Get-Location)).Parent.FullName

    # Validate the destination root folder
    if (-not (Test-Path -Path $DestinationRootFolder)) {
        Write-Error "The provided destination root folder path does not exist: '$DestinationRootFolder'"
        exit 1
    }

    Write-Host "Before folder creation: $(Get-Date)"

    # Create required folders
    $ReleaseUnsignedFolder = Join-Path -Path $DestinationRootFolder -ChildPath "Release_Unsigned"
    $FilesToSignFolder = Join-Path -Path $DestinationRootFolder -ChildPath "FilesToSign"
    $SignedFilesFolder = Join-Path -Path $DestinationRootFolder -ChildPath "SignedFiles"
    $ReleaseSignedFolder = Join-Path -Path $DestinationRootFolder -ChildPath "Release_Signed_Prep"

    New-Item -ItemType Directory -Path $ReleaseUnsignedFolder -Force
    New-Item -ItemType Directory -Path $FilesToSignFolder -Force
    New-Item -ItemType Directory -Path $SignedFilesFolder -Force
    New-Item -ItemType Directory -Path $ReleaseSignedFolder -Force


    Write-Host "Before Get-ChildItem: $(Get-Date)"

    # Get all project folders excluding the specified folders
    $projectFilesUnsigned = @()
    $projectFilesSigned = @()


    # Get all items in the SQLNexus \Bin\Release directory
    $releaseBinaries = $Nexus_RootDir + "\SQLNexus\Bin\Release"
    $allItems = Get-ChildItem -Path $releaseBinaries -Recurse

    # Loop through each item and exclude folders and their contents, as well as specific files
    foreach ($item in $allItems) {

            # filter out folders and files that are in the unsigned ExcludeFolders and ExcludeFiles arrays
            # and add them to the projectFilesUnsigned array
            $excludeUnSig = $false
            foreach ($excludeFolder in $ExcludeFoldersUnsignedArr) {
                if ($item.FullName -like "*$excludeFolder*") {
                    $excludeUnSig = $true
                    break
                }
            }
            foreach ($excludeFile in $ExcludeFilesUnsignedArray) {
                if ($item.Name -eq $excludeFile) {
                    $excludeUnSig = $true
                    break
                }
            }
            if (-not $excludeUnSig) {
                $projectFilesUnsigned += $item
            }
        
        
        # filter out folders and files that are in the signed ExcludeFolders and ExcludeFiles arrays
        # and add them to the projectFilesSigned array
        $excludeSig = $false
        foreach ($excludeFolder in $ExcludeFoldersSignedArr) {
            if ($item.FullName -like "*$excludeFolder*") {
                $excludeSig = $true
                break
            }
        }
        foreach ($excludeFile in $ExcludeFilesSignedArray) {
            if ($item.Name -eq $excludeFile) {
                $excludeSig = $true
                break
            }
        }
        if (-not $excludeSig) {
            $projectFilesSigned += $item
        }
    }

    # Get only the name of the binaries from the SQLNexus project
    # Filter project files to include only those in the SourceFiles array
    $nexusProjectBinFiles = $projectFilesUnsigned | Where-Object { $_.Name -in $SourceFiles }
    $projectFilesName = $nexusProjectBinFiles.Name

    
    # Check for files that are in the SourceFiles array but not in the project directory
    # and files that are in the project directory but not in the SourceFiles array
    $missingInSourceFiles = $projectFilesName | Where-Object { $_ -notin $SourceFiles }
    $missingInProjectFiles = $SourceFiles | Where-Object { -not (Test-Path (Join-Path -Path $releaseBinaries -ChildPath $_)) }

    if ($missingInSourceFiles -or $missingInProjectFiles) {
        if ($missingInSourceFiles) {
            Write-Host "Files missing in SourceFiles array:`r`n  $($missingInSourceFiles -join "`r`n  ")" -ForegroundColor Red
        }
        if ($missingInProjectFiles) {
            Write-Host "Files missing in project directory:`r`n  $($missingInProjectFiles -join "`r`n  ")" -ForegroundColor Red
        }
        exit 1
    }



    $beforeCopy = $(Get-Date)
    Write-Host "Before Copy-Item in FilesToSign: $beforeCopy"

    # Copy each of the SQLNexus project binaries that need signing to the FilesToSign folder
    $nexusProjectBinFiles | ForEach-Object { Copy-Item -Path $_.FullName -Destination $FilesToSignFolder }

    Write-Host "`r`nTotal files copied to the 'FilesToSign' folder: $($nexusProjectBinFiles.Count)"

    Write-Host "`r`nStarting to copy files to the 'Release_Unsigned'  folders..."
    Write-Host "`----------------------------------------------------------------"

    Write-Host "Before Copy-Item in Signed and Unsigned folders: $(Get-Date)"
    $copiedFilesCount = 0
    # Copy all files to the Release_Unsigned folder, preserving the folder structure
    $projectFilesUnsigned | ForEach-Object {

        # extract the relative path from the parent directory to the destination folder
        # and create the corresponding destination path for Release_Signed_Prep and Release_Unsigned folders
        $relativePath = $_.FullName.Substring($Nexus_RootDir.TrimEnd('\').Length + 1)
        $destReleaseUnsignedPath = Join-Path -Path $ReleaseUnsignedFolder -ChildPath $relativePath
        
        # copy to Release_Unsigned folder as well
        Write-Host "Copying file: '$($_.FullName)' to '$destReleaseUnsignedPath'"
        Copy-Item -Path $_.FullName -Destination $destReleaseUnsignedPath -Force

        # Increment the copied files count for each file copied
        $copiedFilesCount++
    }

    Write-Host "`r`nTotal files copied to the 'Release_Unsigned' folder': $copiedFilesCount"

    Write-Host "`r`nStarting to copy files to the 'Release_Signed_Prep' folders..."
    Write-Host "`----------------------------------------------------------------"

    # reset the copied files count for the next copy operation
    $copiedSignedFilesCount = 0

    # Copy all files to the Release_Signed_Prep folder, preserving the folder structure
    $projectFilesSigned | ForEach-Object {

        # extract the relative path from the parent directory to the destination folder
        # and create the corresponding destination path for Release_Signed_Prep and Release_Unsigned folders
        $relativePath = $_.FullName.Substring($Nexus_RootDir.Length + 1)
        $destReleaseSignedPrepPath = Join-Path -Path $ReleaseSignedFolder -ChildPath $relativePath
        
        # copy to Release_Signed_Prep folder, preserving the folder structure
        Write-Host "Copying file: '$($_.FullName)' to '$destReleaseSignedPrepPath'"
        Copy-Item -Path $_.FullName -Destination $destReleaseSignedPrepPath -Force

        # Increment the copied files count for each file copied
        $copiedSignedFilesCount++
    }

    Write-Host "`r`nTotal files copied to the 'Release_Signed_Prep' folder: $copiedFilesCount"


    Write-Host "After Copy-Item: $(Get-Date). It took $((New-TimeSpan -Start $beforeCopy -End (Get-Date)).TotalSeconds) seconds to copy files."
    Write-Host "`r`nList of files in the  '$FilesToSignFolder' folder:"
    Get-ChildItem -Path $FilesToSignFolder | Select-Object -Property Name, LastWriteTime, @{Name="LengthKB";Expression={[math]::Round($_.Length / 1KB, 2)}} | Format-Table -AutoSize

}
catch {
    Write-Host "An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Script line number: $($_.InvocationInfo.ScriptLineNumber)" -ForegroundColor Red
  
    exit 1
}
finally {
    Write-Host "`nSummary:"
    Write-Host "=========="
    Write-Host "Total files from source folder: $($allItems.Count)"
    Write-Host "Total files copied to 'FilesToSign': $($nexusProjectBinFiles.Count)"
    Write-Host "Total files copied to 'Release_Unsigned': $copiedFilesCount"
    Write-Host "Total files copied to 'Release_Signed': $copiedSignedFilesCount"
    Write-Host "`r`nScript execution completed."
}