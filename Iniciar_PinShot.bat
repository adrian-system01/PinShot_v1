@echo off
setlocal
title PinShot

set "INDEPENDENT_EXE=%~dp0dist_independiente\PinShot.exe"
set "LOCAL_EXE=%~dp0dist\PinShot.exe"

if exist "%INDEPENDENT_EXE%" (
    start "" "%INDEPENDENT_EXE%"
    exit /b 0
)

if exist "%LOCAL_EXE%" (
    start "" "%LOCAL_EXE%"
    exit /b 0
)

echo No se encontro una copia ya compilada de PinShot.
echo.
echo Abriendo el compilador independiente...
call "%~dp0Compilar_Independiente.bat"
