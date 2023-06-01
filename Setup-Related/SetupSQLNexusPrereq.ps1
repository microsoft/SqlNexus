# 04/2017 Joseph.Pilov  - Initial build
# 02/2021 Joseph.Pilov  - Update with PowerBI installation and web downloads
# 05/2023 Joseph.Pilov -  Update with .NET Framework 4.8 installation and change RML download URL



function DownloadNexusPrereqFile ([string] $url, [string] $destination_file)
{
    #start the download - asynchronously
    $client = new-object System.Net.WebClient
    $client.DownloadFile($url,$destination_file)
}



Write-Host "Checking for 'System CLR Types for SQL' installation"

#check for the 64-bit version first
$cLR_sw_found64bit = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName | 
                     where {$_.DisplayName -Match 'clr types' -and ($_.DisplayVersion -ne $null) -and ($_.DisplayVersion.Substring(0,2) -as [int]) -eq 13}

if ($cLR_sw_found64bit  -eq $null)
 {
    #check the 32-bit reg key for installed component
    $cLR_sw_found32bit = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName | 
                     where {$_.DisplayName -Match 'clr types' -and ($_.DisplayVersion.Substring(0,2) -as [int]) -eq 13}

    if ($cLR_sw_found32bit -eq $null)
    {
		$CLR_installation_file = "$env:temp\SQLSysClrTypes.msi"
        Write-Host "  Downloading SQLSysCLRTypes from the Web to $CLR_installation_file....." -BackgroundColor DarkYellow
		DownloadNexusPrereqFile -url "https://download.microsoft.com/download/B/1/7/B1783FE9-717B-4F78-A39A-A2E27E3D679D/ENU/x64/SQLSysClrTypes.msi" -destination_file $CLR_installation_file
		Start-Sleep -Seconds 1
		Write-Host "  Launching SQL CLR Types installation" -BackgroundColor DarkYellow
		Start-Process -FilePath "msiexec" -ArgumentList "/i $CLR_installation_file /lv $env:temp\SQLSysClrTypes_Install.log" -Wait
		Write-Host "  Removing the downloaded SQL CLR Types installation file" -BackgroundColor DarkYellow
		Remove-Item -Path $CLR_installation_file
    }

    else
    {
        Write-Host "  The required 32-bit version 13.x of 'Microsoft System CLR Types for SQL Server' is already installed. Version (s) found: ", $cLR_sw_found32bit.DisplayVersion -BackgroundColor DarkGreen
    }
 }
else 
 {
    Write-Host "  The required 64-bit version 13.x of 'Microsoft System CLR Types for SQL Server' is already installed. Version(s) found,", $cLR_sw_found64bit.DisplayVersion -BackgroundColor DarkGreen
 }

#*******************************************************************************************************
#install Report Viewer
Write-Host "Checking for Report Viewer installation"

$rep_viewer_installation_file = "$env:temp\ReportViewer.msi"

$rViewer_sw_found = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName |
                     where {$_.DisplayName -Match 'Report Viewer' -and ($_.DisplayVersion.Substring(0,2) -as [int]) -ge 13}

if ($rViewer_sw_found -eq $null)
 {
    #check the 32-bit reg key
    $rViewer_sw_found_32bit = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* |
                         Select-Object DisplayVersion , DisplayName | 
                         where {$_.DisplayName -Match 'Report Viewer' -and ($_.DisplayVersion.Substring(0,2) -as [int]) -ge 13}

    if ($rViewer_sw_found_32bit -eq $null)
     {
		
		Write-Host "  Downloading Report Viewer Control from the Web to $rep_viewer_installation_file....." -BackgroundColor DarkYellow
		DownloadNexusPrereqFile -url "https://download.microsoft.com/download/B/1/7/B1783FE9-717B-4F78-A39A-A2E27E3D679D/ENU/x86/ReportViewer.msi" -destination_file  $rep_viewer_installation_file
		Start-Sleep -Seconds 1
		Write-Host "  Launching Report Viewer installation" -BackgroundColor DarkYellow
		Start-Process -FilePath "msiexec" -ArgumentList "/i $rep_viewer_installation_file /lv $env:temp\ReportViewer_Install.log" -Wait
		Write-Host "  Removing the downloaded Report Viewer installation file" -BackgroundColor DarkYellow
		#Remove-Item -Path $rep_viewer_installation_file
     }
    else
     {
        Write-Host "  The minimum required 32-bit version of 'Microsoft Report Viewer Runtime' is already installed. Version (s) found: ", $rViewer_sw_found_32bit.DisplayVersion -BackgroundColor DarkGreen
     }
 }
 else
 {
    Write-Host "  The minimum required version of 'Microsoft Report Viewer Runtime' is already installed. Version (s) found: " , $rViewer_sw_found.DisplayVersion -BackgroundColor DarkGreen
 }

#*******************************************************************************************************
#install RML Utilities
Write-Host "Checking for 'RML Utilities' installation"

$RMLsw_found64bit = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName | 
                     Where {$_.DisplayName -Match 'rml utilities for SQL'}

# take the version which is in this format 09.04.0102 and split it into an array where element 0 would be 09, element 1 woudl be 04, etc.
if ($RMLsw_found64bit)
{
    $ver_array = $RMLsw_found64bit.DisplayVersion.Split(".")
}

if ($RMLsw_found64bit -eq $null) #if not there, install it
{
	$rml_utils_installation_file = "$env:temp\RMLSetup_AMD64.msi"
	Write-Host "  Downloading RML Utilities from the Web..." -BackgroundColor DarkYellow
	DownloadNexusPrereqFile -url "https://download.microsoft.com/download/a/a/d/aad67239-30df-403b-a7f1-976a4ac46403/RMLSetup.msi" -destination_file $rml_utils_installation_file
	Start-Sleep -Seconds 1
	Write-Host "  Launching RML Utilities installation" -BackgroundColor DarkYellow
	Start-Process -FilePath "msiexec" -ArgumentList "/i $rml_utils_installation_file /lv $env:temp\RMLSetup_AMD64_Install.log" -Wait
	Write-Host "  Removing the downloaded RML Utilities installation file..." -BackgroundColor DarkYellow
	Remove-Item -Path $rml_utils_installation_file
}

#major version is older than 9.00.00

elseif( ($ver_array[0] -as [int]) -lt 9) #directly check first array member for major version 09.0... 
{
    Write-Host "  You have a very old version of 'RML Utilities' installed. Version found: " , $RMLsw_found64bit.DisplayVersion ". You must uninstall it before you can install the latest version " -BackGroundColor Red
    
    #pop a dialog to ask them to continue or not
    Add-Type -AssemblyName System.Windows.Forms
    $result = [System.Windows.Forms.MessageBox]::Show("Continue with uninstallation of RML Utils version: " + $RMLsw_found64bit.DisplayVersion + "?","Uninstall", "YesNo" , "Information" , "Button1")


    if($result -ne $null)
    {
        if ($result.ToString() -eq 'Yes')
        {
            $uninstallObj = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                             Where {$_.DisplayName -Match 'rml utilities for SQL'}
            $tmpUninstStr = $uninstallObj.UninstallString  -Replace "msiexec.exe","" -Replace "/I","" -Replace "/X",""
            $tmpUninstStr = $tmpUninstStr.Trim()
            start-process "msiexec.exe" -arg "/X $tmpUninstStr" -Wait

            #now install RML Utils
            Write-Host "  Launching RML Utilities installation after uninstallation of previous version" -BackgroundColor DarkYellow
            Start-Process -FilePath "msiexec.exe" -ArgumentList "/i https://download.microsoft.com/download/7/A/D/7ADE5D8B-47AB-4E94-BAD0-5416D6B6D383/RMLSetup.msi" -Wait

        }
        else #result is 'No'
        {
            Write-Host "You must uninstall current version of RML Utils manually before you can install the latest version!" -BackgroundColor DarkYellow
        }
    }
}

#minor version is older than 9.00.102
elseif 
( ($ver_array[2] -as [int]) -lt 102 -and    #just directly check last 2 digits for minor version 09.04.00102
  ($ver_array[0] -as [int]) -ge 9
)
{
    [string]$confirm = $null

    Write-Host "  You don't have that latest version of 'RML Utilities' installed. Version found: " , $RMLsw_found64bit.DisplayVersion ". You must uninstall it before you can install the latest version " -BackGroundColor Red
    
    Write-Host "  Would you like to uninstall the current, download and install the latest version of 'RML Utilities' 9.00.102?" -BackgroundColor DarkYellow

	while (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
	{
			
		$confirm = Read-Host "Download and install latest RML Utilities (Y/N)>" 

		$confirm = $confirm.ToString().ToUpper()
		if (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
		{
			Write-Host ""
			Write-Host "Please chose [Y] to download and install RML Utilities 9.00.102 ."
			Write-Host "Please chose [N] to continue without installing RML Utilities."
			Write-Host ""
		}
	}

	if ($confirm -eq "Y")
	{ 
         #pop a dialog to ask them to continue or not
        Add-Type -AssemblyName System.Windows.Forms
        $result = [System.Windows.Forms.MessageBox]::Show("Continue with uninstallation of RML Utils version: " + $RMLsw_found64bit.DisplayVersion + "?","Uninstall", "YesNo" , "Information" , "Button1")

        if($result -ne $null)
        {
            if ($result.ToString() -eq 'Yes')
            {
                $uninstallObj = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                                 Where {$_.DisplayName -Match 'rml utilities for SQL'}
                $tmpUninstStr = $uninstallObj.UninstallString  -Replace "msiexec.exe","" -Replace "/I","" -Replace "/X",""
                $tmpUninstStr = $tmpUninstStr.Trim()
                start-process "msiexec.exe" -arg "/X $tmpUninstStr" -Wait

                #now install RML Utils
		        $rml_utils_installation_file = "$env:temp\RMLSetup_AMD64.msi"
		        Write-Host "  Downloading RML Utilities from the Web..." -BackgroundColor DarkYellow
		        DownloadNexusPrereqFile -url "https://download.microsoft.com/download/a/a/d/aad67239-30df-403b-a7f1-976a4ac46403/RMLSetup.msi" -destination_file $rml_utils_installation_file
		        Start-Sleep -Seconds 1
		        Write-Host "  Launching RML Utilities installation" -BackgroundColor DarkYellow
		        Start-Process -FilePath "msiexec" -ArgumentList "/i $rml_utils_installation_file /lv $env:temp\RMLSetup_AMD64_Install.log" -Wait
		        Write-Host "  Removing the downloaded RML Utilities installation file..." -BackgroundColor DarkYellow
		        Remove-Item -Path $rml_utils_installation_file

            }
            else #result is 'No'
            {
                Write-Host "You must uninstall current version of RML Utils manually before you can install the latest version!" -BackgroundColor DarkYellow
            }
        }

    }
    elseif ($confirm -eq "N")
    {
        Write-Host "Skipping RML Utilities installation."
    }

  
    
}
else
 {
    Write-Host "  The minimum required version of 'RML Utilities' is already installed. Version (s) found: " , $RMLsw_found64bit.DisplayVersion -BackgroundColor DarkGreen
 }

#*********************************************************************************************************************
#Install .NET Framework 4.8

#check for presence of .NET 4.8 and if not there install it

$DotNet48IsInstalled = $false

$dotNetVersion = Get-ChildItem "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP" -Recurse |
    Get-ItemProperty -name Version -ErrorAction "SilentlyContinue" |
    Where-Object { $_.PSChildName -notmatch '^S'} |  # used to filter out .NET Framework versions that have names starting with 'S' (e.g., 'Servicing' versions), as they are not actual installed versions of .NET Framework.
    Sort-Object -Property Version |
    Select-Object -ExpandProperty Version 

#check for a version that contains 4.8 string, e.g. '4.8.09032'

$DotNet48IsInstalled = foreach ($dotnetver in $dotNetVersion)
{
    if ($dotnetver -match "4.8")
    {
        $true
        break
    }
}


if (-not $DotNet48IsInstalled) 
{
    Write-Host ".NET Framework 4.8 is not installed."

    [string] $confirm = $null
    $DotNet48_installation_file = "$env:temp\ndp48-web.exe"
    $dotNetInstallerUrl = "https://go.microsoft.com/fwlink/?LinkId=2085155"

    Write-Host "Would you like to download and install .Net Framework 4.8?"

	while (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
	{
			
		$confirm = Read-Host "Install .NET Framework 4.8 (Y/N)>" 

		$confirm = $confirm.ToString().ToUpper()
		if (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
		{
			Write-Host ""
			Write-Host "Please chose [Y] to download and install .NET Framework 4.8."
			Write-Host "Please chose [N] to continue without installing .NET Framework 4.8."
			Write-Host ""
		}
	}

	if ($confirm -eq "Y")
	{ 
        Write-Host "  Downloading .NET Framework 4.8 from the Web to $DotNet48_installation_file....." -BackgroundColor DarkYellow
        DownloadNexusPrereqFile -url $dotNetInstallerUrl -destination_file $DotNet48_installation_file
        Start-Sleep -Seconds 1

        Write-Host "  Launching .NET Fraemwork 4.8 installation" -BackgroundColor DarkYellow
        # Install .NET Framework 4.8
        Start-Process -FilePath $DotNet48_installation_file -ArgumentList "/norestart" -Wait
    
        Write-Host "  Removing the downloaded .NET Framework 4.8 installation file $DotNet48_installation_file" -BackgroundColor DarkYellow
        Remove-Item -Path $DotNet48_installation_file
    }
    elseif ($confirm -eq "N")
    {
        Write-Host "Skipping .NET Framework 4.8 installation."
    }
} 
else 
{
    Write-Host ".NET Framework 4.8 is already installed."
}


 
#*********************************************************************************************************************
#Install PowerBI Desktop - optional
 
Write-Host "Checking for 'PowerBI Desktop' installation"

#check for the presence of PowerBI

$PBIDesktop_installation_file = "$env:temp\PBIDesktopSetup_x64.exe"

$PBIDesktop_sw_found = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName | 
                     where {$_.DisplayName -Match 'Microsoft PowerBI Desktop (x64)' -and ($_.DisplayVersion -ne $null)}

if ($PBIDesktop_sw_found  -eq $null)
 {
    #check the 32-bit reg key for installed component
    $PBIDesktop_sw_found32bit = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* |
                     Select-Object DisplayVersion , DisplayName | 
                     where {$_.DisplayName -Match 'Microsoft PowerBI Desktop' -and ($_.DisplayVersion -ne $null)}

    if ($PBIDesktop_sw_found32bit -eq $null)
    {
		$destination = "$env:temp"
		[string]$confirm = $null
		
		Write-Host "Microsoft Power BI Reports are now integrated with SQL Nexus. Would you like to install Microsoft Power BI Desktop to use this functionality?"
		
		while (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
		{
			
			$confirm = Read-Host "Install PowerBI Desktop (Y/N)>" 

			$confirm = $confirm.ToString().ToUpper()
			if (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
			{
				Write-Host ""
				Write-Host "Please chose [Y] to install PowerBI Desktop."
				Write-Host "Please chose [N] to continue without installing PowerBI Desktop."
				Write-Host ""
			}
		}

		if ($confirm -eq "Y")
		{ 
	
			if ($false -eq (Test-Path $PBIDesktop_installation_file))
			{
				Write-Host "Downloading PowerBi Destkop installation file locally ..." -BackgroundColor DarkYellow
				DownloadNexusPrereqFile -url "https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup_x64.exe" -destination_file $PBIDesktop_installation_file
			}
			
			if (Test-Path $PBIDesktop_installation_file)
			 {
				Write-Host "PBIDesktopSetup_x64.exe successfully downloaded to local %temp% folder"
			 }
			else
			 {
				Write-Host "PBIDesktopSetup_x64.exe not present in local %temp% folder. Downloading the file has failed!"
			 }


			Write-Host "  Launching PBIDesktop (64-bit) Installation. Please follow the installation wizard" -BackgroundColor DarkYellow
			Start-Process -FilePath $PBIDesktop_installation_file -Wait

			Write-Host "PowerBI Desktop installation process is complete" -BackgroundColor DarkGreen

			$confirm = ""
			
			Write-Host "If you have successfully installed PowerBI Desktop, choose 'Y' to delete the installation file. 'N' to keep the file around"
			
			while (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
			{
				
				$confirm = Read-Host "Delete $destination\PBIDesktopSetup_x64.exe (Y/N)>" 

				$confirm = $confirm.ToString().ToUpper()
				if (-not(($confirm -eq "Y") -or ($confirm -eq "N") -or ($null -eq $confirm)))
				{
					Write-Host ""
					Write-Host "Please chose [Y] to delete installation file."
					Write-Host "Please chose [N] to keep the file in the %temp% folder."
					Write-Host ""
				}
			}

			if ($confirm -eq "Y")
			{ 
				Remove-Item -Path $PBIDesktop_installation_file
			}
		
		}
		
		else
		{
			Write-Host "You can install PowerBI Desktop (x64) in the future from the following location: https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup_x64.exe" -BackgroundColor DarkYellow
		}

    }

    else
    {
        Write-Host "  The PowerBI Desktop Client already installed. Version found:", $PBIDesktop_sw_found32bit.DisplayVersion -BackgroundColor DarkGreen
    }
 }
else 
 {
    Write-Host "  The PowerBI Desktop Client already installed. Version found:", $PBIDesktop_sw_found.DisplayVersion -BackgroundColor DarkGreen
 }
