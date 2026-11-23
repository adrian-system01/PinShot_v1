# PinShot

PinShot es una aplicación de escritorio en C#/.NET 10 con interfaz Avalonia. Las notas y sus imágenes se almacenan localmente; el formato de notas y los respaldos ZIP se mantienen compatibles con la versión anterior de Windows.

## Uso

1. Inicia PinShot y déjalo abierto.
2. Usa la herramienta de captura del sistema y copia la imagen al portapapeles.
3. PinShot detecta la imagen, abre una ventana flotante y cierra la captura flotante anterior sin guardarla.
4. Guarda la captura desde su ventana o crea y edita notas desde la biblioteca.

Atajos habituales del sistema:

- Windows: `Win+Mayús+S` (elige copiar al portapapeles).
- Linux: `Impr Pant` o la herramienta de captura de tu escritorio, configurada para copiar.
- macOS: `Cmd+Mayús+4`; usa una herramienta que copie la captura al portapapeles si el sistema la guarda como archivo.

La aplicación sondea el portapapeles mientras está abierta; no instala un capturador global propio. Avalonia aporta un menú de bandeja donde la plataforma lo soporta; en macOS el clic de la bandeja no está soportado, aunque el menú nativo sí. El inicio automático y los atajos de captura nativos todavía no están portados.

## Compilar

```powershell
dotnet test PinShot.Tests
dotnet build PinShot.Desktop -c Release
```

Publica de forma autocontenida para la arquitectura deseada:

```text
cd PinShot.Desktop
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

En Windows también están disponibles `Compilar.bat` y `Compilar_Independiente.bat`. La compilación local se ha verificado en Windows; la matriz de CI valida los demás sistemas cuando se ejecuta en GitHub Actions.

## Exportar imágenes

Desde la ventana de una captura, el menú de exportación permite guardar PNG, TIFF o OpenEXR. PNG conserva exactamente los bytes originales; TIFF se escribe sin pérdida con compresión Deflate y canales RGBA de 8 bits. OpenEXR se escribe como Half-float RGBA lineal: convierte los canales RGB de sRGB a lineal y conserva el alfa. Como las capturas actuales son SDR de 8 bits, exportarlas como EXR no crea información HDR ni recupera detalle adicional. Los avisos de licencia de las bibliotecas de formatos están en [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md).

## Hardware y recursos

En Configuración, PinShot muestra el sistema operativo, arquitectura, CPU, RAM y resultado de la comprobación opcional de Vulkan (`vulkaninfo`). Los perfiles **Ligero**, **Equilibrado** y **Rendimiento** calculan límites conservadores de memoria, caché y concurrencia; como aún no hay tareas de IA integradas, esos límites todavía no gobiernan un procesamiento real. Detectar Vulkan no implica que AI Enhance esté disponible: el motor Real-ESRGAN aún no está integrado.

## Datos locales

Los datos están bajo `Environment.SpecialFolder.LocalApplicationData/PinShot`:

- `Notas/`: una carpeta por nota con `nota.json` e imágenes PNG.
- `config.json`: apariencia y perfil de recursos.
- `log.txt`: errores operativos.

Usa **Configuración > Exportar ZIP** para mover o respaldar notas. La importación agrega notas con IDs nuevos y no reemplaza las existentes.

## Proyectos

- `PinShot.Core`: almacenamiento, formatos de datos, detección de hardware y política de recursos sin WPF.
- `PinShot.Desktop`: interfaz Avalonia para Windows, Linux y macOS.
- `PinShot`: cliente WPF anterior, conservado temporalmente como compatibilidad de Windows.
- `PinShot.Tests`: pruebas portables del núcleo.

