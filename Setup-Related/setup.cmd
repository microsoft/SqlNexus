@echo off 
rem
rem   Joseph Pilov  03/2021
rem 
echo.
echo.     =======================================================
echo.     THIS WILL INSTALL EACH PREREQUISITE FOR SQL NEXUS: 
echo.        SQLSysClrTypes, ReportViewer, RML Utilities
echo.     IF YOU ALREADY HAVE ONE OF THESE PREREQUISITES INSTALLED, 
echo.     THE INSTALLATION FOR THAT COMPONENT WILL BE SKIPPED
echo.     =======================================================
pause
echo.  Installing SQLNexus prerequisites


powershell.exe -version 3.0 -ExecutionPolicy Bypass -File .\SetupSQLNexusPrereq.ps1

echo.
echo.=======================================================
echo.Installing SQLNexus now
echo.=======================================================
start /wait .\setup.exe
echo.
echo.Finished executing setup script!
echo.
pause