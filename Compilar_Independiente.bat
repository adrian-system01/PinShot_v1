@echo off
setlocal
title Compilar PinShot independiente
REM Version que NO necesita tener .NET instalado (ocupa mas espacio).
cd /d "%~dp0PinShot.Desktop"
echo Compilando PinShot (independiente)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "%~dp0dist_independiente"
if %errorlevel% neq 0 (
    echo.
    echo ERROR al compilar. Revisa los mensajes de arriba.
    pause
    exit /b %errorlevel%
)
echo.
echo Listo! Ejecutable: %~dp0dist_independiente\PinShot.exe
start "" "%~dp0dist_independiente\PinShot.exe"
pause
