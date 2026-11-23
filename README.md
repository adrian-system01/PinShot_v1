# PinShot

Aplicación de escritorio con Avalonia y .NET 10.

## Paquete de Windows

Para compartir PinShot sin el código fuente ni ejecutables antiguos, distribuye `PinShot-Windows.zip`. Quien lo reciba debe extraer todos los archivos y ejecutar `PinShot.exe`. El ejecutable incluye el icono del proyecto y el runtime; no necesita instalar .NET. El ZIP también contiene los avisos de licencia de terceros.

Si descargas el código fuente, inicia la aplicación con `Iniciar_PinShot.bat`. El icono y el logo están en `PinShot\` y se incluyen en el programa al compilarlo.

## Compilar desde el código fuente

Se necesita el SDK de .NET 10. En Windows, ejecuta:

- `Compilar.bat`: publica usando el runtime de .NET instalado.
- `Compilar_Independiente.bat`: incluye el runtime para ejecutar sin instalar .NET.

Comandos equivalentes:

```powershell
dotnet test .\PinShot.Tests\PinShot.Tests.csproj
dotnet publish .\PinShot.Desktop\PinShot.Desktop.csproj -c Release -r win-x64 --self-contained true
```

Para compilar otras plataformas, cambia el identificador de runtime (`-r`):

- Linux: `linux-x64`
- macOS Intel: `osx-x64`
- macOS Apple Silicon: `osx-arm64`
