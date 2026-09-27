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

---

# Mundo 1 — `balance_w1.json` · `campaign_w1.json` · `level_m1_n*.json`

El Mundo 1 (`TL-002`) tiene su propio archivo de balance: el P0 (`balance_p0.json`, `level_p0.json`) queda congelado en 1.0 y se sigue abriendo desde el menú como *Laboratorio* (`PRJ-012`). La versión del Mundo 1 viaja en cada registro como `balance_version` y es también la versión del producto (`PlayerSettings.bundleVersion`).

| Versión | Fecha | Qué cambió | Problema que intenta resolver | Resultado medido |
|---|---|---|---|---|
| w1-0.1 | 2026-09-27 | Línea base del Mundo 1: torres iniciales y D/E/V/A del Doc 05 sin tocar; seis torres, tres enemigos, economía persistente y seis niveles como hipótesis | — (línea base) | simulador: los seis niveles se ganan con 3★ con un plan competente en 5:25 / 6:32 / 7:19 / 8:28 / 9:37 / 11:25; Arqueras solas ganan 1–5 y pierden el 6; Cañón solo pierde el 5 y el 6 (`LDS-002.6`). Pendiente de playtest |

## Valores del Mundo 1 que no vienen de los documentos

Todos son hipótesis de Game Design o Level Design (`PRJ-013`); el supuesto dice por qué hizo falta cada uno.

| Campo | Valor N1 → N2 | Fuente |
|---|---|---|
| Mortero: costo, daño, cadencia, alcance, mínimo, área, vuelo | 175 · 90 → 125 · 3,0 → 2,7 s · 11 → 12 · 3,5 · 2,0 → 2,3 · 1,1 s; metal 0,5 | `GDS-002.4`, S9–S10 |
| Bombardera | 150 · 45 → 60 · 1,6 → 1,45 s · 6 → 6,5 · área 1,5 → 1,8 · 12 u/s; metal 0,5 | `GDS-002.4`, S9 |
| Eléctrica | 140 · 16 → 20 · 0,55 → 0,5 s · 4,5 → 5 · rebotes 3 → 4 · radio 3,0 → 3,4; metal 0 | `GDS-002.4`, S9, S11 |
| Infernal | 200 · etapas de 1 s: 20/45/90/160 → 25/55/110/200 de daño por segundo · 6 → 6,5; metal 1,0 solo en la etapa máxima | `GDS-002.4`, S9, S12 |
| Torre de oro | 150 · 1,25 → 2,0 oro/s · tope 50 → 90 | `GDS-002.4`, S9, S15–S16 |
| Lanzallamas | 175 · 30 → 40 · 2,4 → 2,2 s · 5,5 → 6 · ancho 1,3 → 1,5 · quemadura 12 → 16 por s durante 3 s; metal 0 | `GDS-002.4`, S9, S13–S14 |
| `timing.burnTick` | 0,25 s | `GDS-002.4`, S14 |
| Duende tanque `T` | vida 550 · recorrido 48 s · daño 25 · oro 35 · pesado | `GDS-002.5`, S17 |
| Duende gigante `G` | vida 3200 · 70 s · daño 50 · oro 150 · pesado, miniboss | `GDS-002.5`, S17 |
| Bebé dragón `B` | aire · vida 2400 · 50 s · daño 50 · oro 150 · miniboss | `GDS-002.5`, S17 |
| Moneda persistente | *Cristales*; base por nivel 40/50/70/80/90/120; ×0,50/0,75/1,00 (Doc 05 §9); repetición ×0,25 + diferencia por récord | `GDS-002.3`, S18, S22 |
| Tienda | dos mejoras por torre desbloqueable, 60–90 cristales | `GDS-002.3`, S18 |
| Tanda | Arqueras y Cañón: daño +15 % y alcance +8 %; Mago: daño +15 % y área +8 %; acumulable | `GDS-002.3`, S18 |
| Oleadas y recorridos | seis niveles, 8–11 oleadas, grupos por carril, miniboss en la última | `LDS-002.6`, S19, S21 |
