@echo off
setlocal
title PinShot

set "INDEPENDENT_EXE=%~dp0dist_independiente\PinShot.exe"
set "LOCAL_EXE=%~dp0dist\PinShot.exe"
set "PINSHOT_ROOT=%~dp0"

if exist "%INDEPENDENT_EXE%" (
    set "EXE=%INDEPENDENT_EXE%"
    goto comprobar
)

if exist "%LOCAL_EXE%" (
    set "EXE=%LOCAL_EXE%"
    goto comprobar
)

goto compilar

:comprobar
set "PINSHOT_EXE=%EXE%"
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $r=$env:PINSHOT_ROOT; $e=$env:PINSHOT_EXE; $b=(Get-Item -LiteralPath $e).LastWriteTimeUtc; $i=@((Join-Path $r 'PinShot.Desktop'),(Join-Path $r 'PinShot.Core')); $f=@(Get-ChildItem -LiteralPath $i -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }); $f+=@(Get-Item -LiteralPath (Join-Path $r 'PinShot\PinShot.ico'),(Join-Path $r 'PinShot\PinShot_logo.png')); $stale=$f | Where-Object { $_.LastWriteTimeUtc -gt $b }; if (-not $f -or $stale) { exit 1 }"
if errorlevel 1 goto compilar

start "" "%EXE%"
exit /b 0

:compilar
echo La copia compilada no existe o esta desactualizada.
echo.
echo Compilando la version actual...
call "%~dp0Compilar_Independiente.bat"
exit /b %errorlevel%
