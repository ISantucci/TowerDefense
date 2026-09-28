# ClashDefenseSim — medir el P0 y el Mundo 1 sin abrir Unity

Compila **el mismo núcleo** que usa el juego (`Assets/ClashDefense/Scripts/Core`) y lo corre en consola. Sirve para probar reglas y balance antes de jugar (Doc 05 §1: todo cambio con su motivo y su resultado).

Requiere el SDK de .NET 8. Desde esta carpeta:

```txt
dotnet run test     pruebas del núcleo contra los criterios de validación de GDS-001.0 … GDS-001.6
dotnet run bots     jugadores automáticos con distintos planes; resultado, estrellas, duración y filtraciones
dotnet run mapa     cobertura del recorrido por lugar construible (instrumento de LDS-001.1)
dotnet run lds      presión de la oleada 1 según dónde va la primera Arqueras


Mundo 1 (TL-002):
dotnet run test     además de las del P0, 49 pruebas del Mundo 1: torres nuevas, recorridos, duración por nivel, progresión
dotnet run mundo1   los seis niveles con cinco estrategias de bot (competente, sin mejoras, solo Arqueras, solo Cañón, lento)
dotnet run oleadas <carpeta de datos> m1_n3   duración, filtrados y profundidad de cada oleada de un nivel (instrumento de LDS-002.6)
```

Los mapas y las oleadas del Mundo 1 se generan con `Tools/ClashDefenseLevels` (Python): `levels.py` escribe los `level_m1_n*.json`, `draw.py` dibuja `mapas_mundo1.png`.

Los datos están en `Datos/`: los exporta Unity desde los assets del juego (menú *Clash Defense › Datos › Exportar*, y automáticamente al guardar una escena de nivel). No se editan a mano: el balance se cambia en los assets de `Assets/ClashDefense/Data` y se vuelve a exportar.

Para probar otro balance sin tocar el del juego: copiá la carpeta `Datos` a otro lado, cambiá los números y pasá la carpeta como segundo argumento: `dotnet run bots C:\ruta\a\mis_datos`.

Los bots son **cota optimista**: construyen en el mejor lugar libre apenas les alcanza el oro. No reemplazan el playtest.
