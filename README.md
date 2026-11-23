# PinShot

Aplicacion para Windows que convierte las capturas del portapapeles en ventanas flotantes y guarda notas en el equipo.

## Uso diario

1. Haz doble clic en **Iniciar PinShot.lnk**.
2. Busca el icono de PinShot en el area de notificaciones (la flecha junto al reloj).
3. Pulsa `Win+Mayus+S` para realizar una captura.

El iniciador usa primero la copia independiente de `dist_independiente\`; si no existe, usa `dist\`. Si no hay ninguna copia, abre el compilador independiente. No hace falta instalar .NET para ejecutar la copia independiente.

## Compilacion para desarrollo

- `Compilar.bat`: compila una version que requiere el runtime de .NET.
- `Compilar_Independiente.bat`: compila una version independiente que incluye el runtime.

Los ejecutables de prueba se crean en `dist\` o `dist_independiente\`.

## Archivos utiles

- `LEEME.txt`: instrucciones rapidas en castellano.
- `PinShot\README.md`: detalles tecnicos, datos y actualizaciones.
- `%LOCALAPPDATA%\PinShot\Notas`: notas guardadas.
- `%LOCALAPPDATA%\PinShot\log.txt`: registro de errores.

Para iniciar PinShot automaticamente con Windows, abre el menu del icono de la bandeja y selecciona **Iniciar con Windows**.

