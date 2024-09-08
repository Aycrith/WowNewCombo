@echo off
setlocal enabledelayedexpansion

cd /d ..\
set "pwd=%cd%"
cd /d HeadlessServer

set "root=%pwd%\JSON\class\"

for /r "%root%" %%f in (*.json) do (
    set "file=%%f"
    set "file=!file:%root%=!"
    echo !file!

    dotnet run -c Release --no-restore -- !file! -m Local --loadonly
)