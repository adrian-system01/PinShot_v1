@echo off
setlocal
title Compilar PinShot
cd /d "%~dp0PinShot.Desktop"
echo Compilando PinShot...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "%~dp0dist"
if %errorlevel% neq 0 (
    echo.
    echo ERROR al compilar. Revisa los mensajes de arriba.
    pause
    exit /b %errorlevel%
)
echo.
echo Listo! Ejecutable: %~dp0dist\PinShot.exe
echo Abriendo PinShot...
start "" "%~dp0dist\PinShot.exe"
pause
