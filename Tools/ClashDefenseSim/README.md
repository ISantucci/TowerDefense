# ClashDefenseSim — medir el P0 y el Mundo 1 sin abrir Unity

Compila **el mismo núcleo** que usa el juego (`Assets/ClashDefense/Scripts/Core`) y lo corre en consola. Sirve para probar reglas y balance antes de jugar (Doc 05 §1: todo cambio con su motivo y su resultado).

Requiere el SDK de .NET 8. Desde esta carpeta:

```txt
dotnet run test     pruebas del núcleo contra los criterios de validación de GDS-001.0 … GDS-001.6
dotnet run bots     jugadores automáticos con distintos planes; resultado, estrellas, duración y filtraciones
dotnet run mapa     cobertura del recorrido por lugar construible (instrumento de LDS-001.1)
dotnet run lds      presión de la oleada 1 según dónde va la primera Arqueras


Mundo 1 (TL-002):
dotnet run test     además de las del P0, 65 pruebas del Mundo 1 contra el Doc 05 v2.0 (torres, oleadas por composición, moneda,
                    insignias). Con datos anteriores a w1-0.2 dan una sola falla que dice qué correr
dotnet run mundo1   los seis niveles con cinco estrategias de bot (competente, sin mejoras, solo Arqueras, solo Cañón, lento)
dotnet run oleadas <carpeta de datos> m1_n3   duración, filtrados y profundidad de cada oleada de un nivel (instrumento de LDS-002.6)
dotnet run economia [carpeta de datos] [--salida carpeta] [--nivel m1_n3]   holgura por escalado del oro, sondas, destino del gasto, ingreso por oleada,
                    indicadores del Doc 05 §11 y §14 que un bot puede medir, y cristales; escribe economia_w1.csv (instrumento de MET-004.1)
```

Los mapas y las oleadas del Mundo 1 se generan con `Tools/ClashDefenseLevels` (Python): `levels.py` escribe los `level_m1_n*.json`, `draw.py` dibuja `mapas_mundo1.png`.

Los datos están en `Datos/`: los exporta Unity desde los assets del juego (menú *Clash Defense › Datos › Exportar*, y automáticamente al guardar una escena de nivel). No se editan a mano: el balance se cambia en los assets de `Assets/ClashDefense/Data` y se vuelve a exportar.

**Propuestas de balance.** `Propuestas/w1-0.2/` es el Doc 05 v2.0 aplicado sobre la exportación (lo arma `generar.py`). Se mide con `dotnet run test Propuestas/w1-0.2` (y lo mismo con `mundo1` y `economia`) hasta que los assets lleven w1-0.2 y Unity exporte `Datos/`; entonces la carpeta se borra.

Para probar otro balance sin tocar el del juego: copiá la carpeta `Datos` a otro lado, cambiá los números y pasá la carpeta como segundo argumento: `dotnet run bots C:\ruta\a\mis_datos`.

Los bots son **cota optimista**: construyen en el mejor lugar libre apenas les alcanza el oro. No reemplazan el playtest.
