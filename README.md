# PinShot

Aplicación de escritorio con Avalonia y .NET 10.

## Paquete de Windows

Para compartir PinShot sin el código fuente ni ejecutables antiguos, distribuye `PinShot-Windows.zip`. Quien lo reciba debe extraer todos los archivos y ejecutar `PinShot.exe`. El ejecutable incluye el icono del proyecto y el runtime; no necesita instalar .NET. El ZIP también contiene los avisos de licencia de terceros.

Si descargas el código fuente, abre `Iniciar_PinShot.lnk` para iniciar PinShot con el icono de la aplicación; el acceso directo ejecuta `Iniciar_PinShot.bat`, que compila `PinShot.Desktop` antes de abrirlo para evitar iniciar una copia antigua. También puedes ejecutar el `.bat` directamente, pero Windows no permite asignarle un icono personalizado. El icono y el logo están en `PinShot\` y se incluyen en el programa al compilarlo.

En la biblioteca y el editor, haz clic en una miniatura para abrir la captura flotante. A tamaño normal, arrástrala para mover la ventana; con zoom, arrastra para recorrer la imagen. `Shift` + arrastrar mueve la ventana incluso con zoom. Usa `Ctrl` + rueda para acercar, doble clic para restablecer y `Alt` + rueda para cambiar la opacidad.

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
