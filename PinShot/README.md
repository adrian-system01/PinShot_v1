# Cliente Windows anterior

Esta carpeta contiene el cliente WPF/WinForms previo y sus adaptadores de Windows. Se conserva temporalmente para compatibilidad y referencia; los scripts de compilación y el pipeline de distribución usan `PinShot.Desktop`, la interfaz Avalonia actual.

El núcleo portable compartido está en `../PinShot.Core`. Los datos mantienen el formato `PinShot/Notas/{id}/nota.json` más imágenes PNG, por lo que las notas existentes y los respaldos ZIP siguen siendo legibles.

La interfaz Avalonia incluye un menú de bandeja donde la plataforma lo soporta. El inicio automático, las actualizaciones Velopack y la captura global específica del sistema siguen pendientes. Consulta el [README principal](../README.md) para el estado y los comandos de compilación multiplataforma.
