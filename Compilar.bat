@echo off
cd /d "%~dp0PinShot"
echo Cerrando PinShot si esta abierto...
taskkill /f /im PinShot.exe >nul 2>&1
echo Compilando PinShot v3.2...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "%~dp0dist"
if %errorlevel% neq 0 (
    echo.
    echo ERROR al compilar. Revisa los mensajes de arriba.
    pause
    exit /b %errorlevel%
)
echo.
echo Listo! Ejecutable: %~dp0dist\PinShot.exe
echo Abriendo PinShot... busca el icono azul en el area de notificaciones (flecha ^).
start "" "%~dp0dist\PinShot.exe"
pause
