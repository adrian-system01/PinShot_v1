@echo off
setlocal
title PinShot

set "OUTPUT_DIR=%~dp0dist_independiente"
set "EXE=%OUTPUT_DIR%\PinShot.exe"

echo Compilando la version actual de PinShot...
dotnet publish "%~dp0PinShot.Desktop\PinShot.Desktop.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "%OUTPUT_DIR%"
if errorlevel 1 (
    echo.
    echo ERROR al compilar PinShot. Revisa los mensajes de arriba.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo.
    echo ERROR: no se encontro el ejecutable compilado: "%EXE%"
    pause
    exit /b 1
)

start "" "%EXE%"
exit /b 0
