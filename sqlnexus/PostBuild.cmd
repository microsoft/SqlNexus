rem "$(ProjectDir)PostBuild.cmd"  "$(SolutionName)"  "$(SolutionDir)\Dependencies"  "$(SolutionDir)\RowsetImportEngine\bin\$(ConfigurationName)"  "$(TargetDir)"  > "$(ProjectDir)PostBuild.log" 2>&1
setlocal ENABLEEXTENSIONS
@echo on
set SOLUTIONNAME=%1
set SRCDIRCODEPLEX=%2
set SRCDIRSQLNEXUS=%3
set TARGETDIR=%4



if /I %SOLUTIONNAME% == "CodePlex" (
echo xcopy /Y %SRCDIRCODEPLEX%  %TARGETDIR%
 xcopy /Y %SRCDIRCODEPLEX%  %TARGETDIR%
)

if /I %SOLUTIONNAME% == "sqlnexus" (
echo xcopy /Y  %SRCDIRSQLNEXUS% %TARGETDIR%
xcopy /Y  %SRCDIRSQLNEXUS% %TARGETDIR%
)

endlocal