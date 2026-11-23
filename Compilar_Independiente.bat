@echo off
REM Version que NO necesita tener .NET instalado (pesa mas)
cd /d "%~dp0PinShot"
taskkill /f /im PinShot.exe >nul 2>&1
echo Compilando PinShot v3.2 (independiente)...
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
