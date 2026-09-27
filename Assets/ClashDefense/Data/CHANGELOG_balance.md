# Registro de cambios del balance — Prototipo 0

Doc 05 §1: "Una modificación realizada durante las pruebas debe registrarse con su motivo y resultado. Cambiar un número sin indicar qué problema intenta resolver invalida la comparación entre versiones."

Cada cambio sube la versión de `balance_p0.json` y agrega una entrada acá. La versión viaja en cada registro de partida.

| Versión | Fecha | Qué cambió | Problema que intenta resolver | Resultado medido |
|---|---|---|---|---|
| 1.0 | 2026-09-27 | Valores del Doc 05 v1.0 sin tocar | — (línea base) | pendiente de playtest |

## Valores que no vienen del Doc 05

Los fijó Game Design para poder construir; son hipótesis igual que el resto.

| Campo | Valor | Fuente |
|---|---|---|
| `towers[].projectileSpeed` | 22 / 16 / 14 u/s | `GDS-001.2`, supuesto S3 |
| `towers[].footprintRadius` | 0,8 u | `GDS-001.2` |
| `timing.countdownStep` / `defenseDuration` | 0,8 s / 1,0 s | `GDS-001.6`, supuesto S6 |
| `enemies[].counterHint` | textos del aviso de debut | `UXS-001.4` |
