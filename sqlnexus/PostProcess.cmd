@echo off

set SQLServer=%1
set Database=%2
set ImportPath=%3

echo %SQLServer% %Database% %ImportPath%



setlocal
set servername=%1
set filepath=%2

if "%servername%"=="" goto usage
if "%filepath%"=="" goto usage


sqlcmd.exe -S%SQLServer% -E -d%Database% -Q"create table tblPlansTemp (sqlplan xml)"



for /f %%j in ('dir /b "%ImportPath%\*.sqlplan"') do sqlcmd.exe -S%SQLServer%  -E -d%Database% -Q"insert into tblPlansTemp select  cast(cast (BulkColumn as varchar(max)) as xml) sqlplan from openrowset (bulk '%filepath%\%%j',single_blob) as doc"



rem sqlcmd.exe -S%SQLServer% -E -otoptables.out -Q"set QUOTED_IDENTIFIER on; WITH XMLNAMESPACES ('http://schemas.microsoft.com/sqlserver/2004/07/showplan' AS sp)  select distinct  stmt.stmt_details.value ('@Database', 'varchar(max)') 'Database' ,  stmt.stmt_details.value ('@Schema', 'varchar(max)') 'Schema' ,   stmt.stmt_details.value ('@Table', 'varchar(max)') 'table'  from (  select sqlplan from tempdb.dbo.tblPlansTemp) as p       cross apply sqlplan.nodes('//sp:Object') as stmt (stmt_details) "


rem rem sqlcmd.exe -S%SQLServer% -Q"drop table tempdb.dbo.tblPlansTemp"



goto :eof

:usage

echo Usage:  %~n0 servername filepath


endlocal