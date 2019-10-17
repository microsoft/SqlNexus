@REM  To register the collector as a service, open a command prompt, change to this 
@REM  directory, and run: 
@REM  
@REM     SQLDIAG /R /I "%cd%\SQLDiagPerfStats_Trace.XML" /O "%cd%\SQLDiagOutput" /P
@REM  
@REM  You can then start collection by running "SQLDIAG START" from Start->Run, and 
@REM  stop collection by running "SQLDIAG STOP". 



@rem the command below sets sqldiag.exe path.  if your installation is different, adjust accordinly
@rem sql 2008 sqldiag.exe will be able to capture multiple platforms.
@rem any sqldiag will be able to detect 32 bit or 64 bit instances
set SQLDIAGCMD=C:\Program Files\Microsoft SQL Server\100\Tools\Binn\SQLdiag.exe
"%SQLDIAGCMD%" /I "%cd%\SQLDiagReplay2008.xml" /O "%cd%\SQLDiagOutput" /P
