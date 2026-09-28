# ClashDefenseLevels — los seis niveles del Mundo 1 como fuente (LDS-002.6)

La geometría (recorridos, rocas, pista del tutorial) vive en `levels.py`; las oleadas, como recetas, en `spec.py`.
Nada de esto se edita a mano en los JSON: se regenera.

```txt
python3 wavegen.py     spec.py  -> waves.json (secuencias e intervalos)
python3 levels.py      levels.py + waves.json -> propuestas/level_m1_n1..6.json (boceto; no pisa el juego)
python3 draw.py        mapas_mundo1.png (vista cenital de los seis mapas)
```

Después de cambiar algo, medirlo con el simulador (desde `Tools/ClashDefenseSim`):

```txt
dotnet run mundo1 ../ClashDefenseSim/Datos          bots por nivel: resultado, duración, filtraciones, profundidad
dotnet run oleadas ../ClashDefenseSim/Datos m1_n4   duración y profundidad de cada oleada con el plan competente
dotnet run test ../ClashDefenseSim/Datos            pruebas del núcleo (P0 + Mundo 1), incluida la duración objetivo de cada nivel
```

Receta de una oleada: `W(ráfaga, repeticiones, lugares vacíos, segundos de aparición, recorridos, miniboss, cola)`.
Ejemplo: `W("D3 E6 V2", 3, 8, 70, "0 x13 1 x13", boss=(4, "G"), tail=("E8 V3 D3", 3, 8))`.

## Desde TL-003

Los niveles del juego se editan en Unity, cada uno en su escena (`Assets/ClashDefense/Scenes/Niveles`): se mueven los puntos del camino, las rocas y la base con el mouse, y al guardar la escena la forma se hornea en su `LevelDefinition` y se exporta a `Tools/ClashDefenseSim/Datos`. Estas herramientas quedan para bocetar (levels.py escribe en `propuestas/`) y para medir (draw.py y el simulador leen lo exportado).
