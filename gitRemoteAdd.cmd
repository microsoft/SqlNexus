


setlocal

set "_urlOriginal=https://github.com/microsoft/SqlNexus.git"

set "_urlSelf=https://github.com/DanielAdeniji/sqlnexus.git"

rem git remote rm [name of the url you sets on adding]

git remote rm %_urlOriginal%

rem git remote add origin https://github.com/DanielAdeniji/sqlnexus.git

git remote add origin %_urlSelf%

rem git push -u origin master

endlocal