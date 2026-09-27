# ClashDefenseSim — medir el Prototipo 0 sin abrir Unity

Compila **el mismo núcleo** que usa el juego (`Assets/ClashDefense/Core`) y lo corre en consola. Sirve para probar reglas y balance antes de jugar (Doc 05 §1: todo cambio con su motivo y su resultado).

Requiere el SDK de .NET 8. Desde esta carpeta:

```txt
dotnet run test     pruebas del núcleo contra los criterios de validación de GDS-001.0 … GDS-001.6
dotnet run bots     jugadores automáticos con distintos planes; resultado, estrellas, duración y filtraciones
dotnet run mapa     cobertura del recorrido por lugar construible (instrumento de LDS-001.1)
dotnet run lds      presión de la oleada 1 según dónde va la primera Arqueras
```

Para probar otro balance sin tocar el del juego: copiá la carpeta `Assets/ClashDefense/Data` a otro lado, cambiá los números y pasá la carpeta como segundo argumento: `dotnet run bots C:\ruta\a\mis_datos`.

Los bots son **cota optimista**: construyen en el mejor lugar libre apenas les alcanza el oro. No reemplazan el playtest.
