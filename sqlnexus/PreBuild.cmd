@ECHO ON
SET ProjDir=%~1
SET TargetDir=%~2
ECHO ==== ProjDir=%~1
ECHO ==== TargetDir=%~2

ECHO md "%TargetDir%Reports" 
md "%ProjDir%Reports" 

ECHO del "%ProjDir%Reports\*.rdl*"
rem del "%ProjDir%Reports\*.rdl*"

ECHO.
ECHO ==== Staging reports (%ProjDir%..\NexusReports\*.rdl) to project dir ("%ProjDir%Reports)... 
ECHO xcopy /Y "%ProjDir%..\NexusReports\*.rdl" "%ProjDir%Reports"
xcopy /Y "%ProjDir%..\NexusReports\*.rdl" "%ProjDir%Reports"
FOR /F "tokens=*" %%I IN ('dir /b "%ProjDir%Reports\*_C.rdl" ^| findstr -V -I rdlc') DO CALL :DeployReport "%%I" 

rem echo copy /Y "%ProjDir%Reports\Query Hash.rdl" "%ProjDir%Reports\Query Hash.rdlc"
rem copy /Y "%ProjDir%Reports\Query Hash.rdl" "%ProjDir%Reports\Query Hash.rdlc"

REM These reports don't follow the "_C" naming convention for child reports. 
FOR /F "tokens=*" %%I IN ('dir /b "%ProjDir%Reports\UniqueBatchDetails.rdl" ^| findstr -V -I rdlc') DO CALL :DeployReport "%%I" 
FOR /F "tokens=*" %%I IN ('dir /b "%ProjDir%Reports\UniqueBatchTopN.rdl" ^| findstr -V -I rdlc') DO CALL :DeployReport "%%I" 
FOR /F "tokens=*" %%I IN ('dir /b "%ProjDir%Reports\InterestingEvents.rdl" ^| findstr -V -I rdlc') DO CALL :DeployReport "%%I" 
FOR /F "tokens=*" %%I IN ('dir /b "%ProjDir%Reports\Lineage.rdl" ^| findstr -V -I rdlc') DO CALL :DeployReport "%%I" 

ECHO xcopy /Y "%ProjDir%..\NexusReports\*.xml" "%ProjDir%Reports"
xcopy /Y "%ProjDir%..\NexusReports\*.xml" "%ProjDir%Reports"

xcopy /Y "%ProjDir%..\NexusReports\*.pbit" "%ProjDir%Reports"

ECHO xcopy /Y "%ProjDir%..\NexusReports\*.sql" "%ProjDir%Reports"
xcopy /Y "%ProjDir%..\NexusReports\*.sql" "%ProjDir%Reports"

ECHO copy /Y "%ProjDir%..\RowsetImportEngine\TextRowsets.xml" "%ProjDir%TextRowsets.xml"
copy /Y "%ProjDir%..\RowsetImportEngine\TextRowsets.xml" "%ProjDir%TextRowsets.xml"





attrib -r "%ProjDir%*.*" /S

GOTO :eof


:DeployReport
SET ReportFile=%~1
ECHO ==== Renaming child report "%ProjDir%Reports\%ReportFile%" to "%ProjDir%Reports\%ReportFile%C" 
attrib -r "%ProjDir%Reports\%ReportFile%" 
attrib -r "%ProjDir%Reports\%ReportFile%C" 
ECHO del "%ProjDir%Reports\%ReportFile%C" 
del "%ProjDir%Reports\%ReportFile%C" 
ECHO ren "%ProjDir%Reports\%ReportFile%" "%ReportFile%C" 
ren "%ProjDir%Reports\%ReportFile%" "%ReportFile%C" 
GOTO :eof
