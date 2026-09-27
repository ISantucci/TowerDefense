from wavegen import W
# Receta: W(ráfaga, repeticiones, lugares vacíos entre ráfagas, segundos de aparición, recorridos, miniboss, cola)
# Criterio (LDS-002.6): el oro total del nivel acotado (el Doc 05 paga 20 por Duende); la presión sube con ráfagas más densas
# y con enemigos de más vida por oro (Tanque, Blindado, minibosses), y los valles entre ráfagas estiran la duración.
SPEC = {
 # Nivel 1 — solo Duende (Doc 02 §10). Tutorial. 5–7 min.
 "m1_n1": [
  W("D", 10, 0, 20),
  W("D3", 4, 3, 26),
  W("D4", 4, 4, 30),
  W("D5", 4, 5, 34),
  W("D6", 3, 8, 34),
  W("D8", 3, 9, 38),
  W("D10", 3, 11, 42),
  W("D14", 3, 12, 46),
 ],
 # Nivel 2 — entra el Esqueleto (rápido, frágil, en grupo). 6–8 min.
 "m1_n2": [
  W("D", 10, 0, 20),
  W("D2 E2", 4, 3, 26),
  W("E4 D2", 4, 4, 30),
  W("E6 D2", 4, 7, 38),
  W("D2 E8", 3, 12, 42),
  W("E10 D3", 3, 14, 46),
  W("D3 E12", 3, 16, 50),
  W("E14 D4", 3, 18, 54),
  W("E20 D6", 3, 20, 60),
 ],
 # Nivel 3 — entra el Esbirro volador, de a uno en su debut (Doc 04 §12.3); miniboss Duende gigante en la última. 7–9 min.
 "m1_n3": [
  W("D", 10, 0, 20, "0"),
  W("D2 E2", 4, 3, 26, "1"),
  W("D V D", 5, 4, 30, "0 1"),
  W("E3 V E3", 4, 4, 32, "0 x7 1 x7"),
  W("D2 E3 V", 4, 5, 36, "0 1"),
  W("E5 V2 D2", 3, 8, 40, "0 x3 1 x3"),
  W("D3 V2 E7", 3, 9, 44, "0 x12 1 x12"),
  W("E9 V3 D3", 3, 10, 48, "0 x8 1 x8"),
  W("D3 E8 V2", 3, 8, 70, "0 x13 1 x13", boss=(4, "G"), tail=("E8 V3 D3", 3, 8)),
 ],
 # Nivel 4 — entra el Duende tanque; el camino se abre en dos carriles que vuelven a unirse. 8–10 min.
 "m1_n4": [
  W("D", 10, 0, 22, "0 x5 1 x5"),
  W("D2 E3", 4, 3, 26, "0 x5 1 x5"),
  W("D2 T", 4, 5, 30, "0 x3 1 x3"),
  W("E4 V D2", 4, 4, 34, "0 x7 1 x7"),
  W("T D3 E4", 3, 7, 38, "0 x4 1 x4"),
  W("V2 E6 D3 T", 3, 8, 42, "0 x6 1 x6"),
  W("T2 D3 E6", 3, 9, 46, "0 x3 1 x3"),
  W("D3 E6 V2 T4", 3, 12, 50, "0 x7 1 x7"),
  W("T6 E8 V3 D3", 3, 14, 56, "0 x6 1 x6"),
  W("T9 E10 V4 D3", 3, 16, 62, "0 x11 1 x11"),
 ],
 # Nivel 5 — sin enemigo nuevo: la novedad es espacial, dos llegadas a la base. 9–11 min.
 "m1_n5": [
  W("D", 8, 0, 22, "0"),
  W("D E2", 5, 3, 26, "1"),
  W("V D3", 4, 4, 30, "0 x4 1 x4"),
  W("T D3 E3", 4, 6, 40, "0 x7 1 x7"),
  W("E6 V3", 3, 10, 44, "0 x9 1 x9"),
  W("T3 D3 V2 E4", 3, 12, 48, "0 x12 1 x12"),
  W("V4 D3 E8 T2", 3, 13, 52, "0 x17 1 x17"),
  W("T5 E8 V4 D2", 3, 15, 56, "0 x19 1 x19"),
  W("D3 E10 V4 T6", 3, 16, 60, "0 x23 1 x23"),
  W("T8 E10 V5 D3", 3, 18, 64, "0 x13 1 x13"),
  W("T10 E12 V6 D4", 3, 20, 72, "0 x16 1 x16"),
 ],
 # Nivel 6 — entra el Duende blindado (metal); miniboss Bebé dragón en la última. Dos entradas, cuatro recorridos. 10–12 min.
 "m1_n6": [
  W("D", 8, 0, 22, "0 1"),
  W("E3 D", 5, 3, 26, "2 3"),
  W("D2 A", 4, 5, 30, "0 x3 3 x3"),
  W("T E3 A V", 4, 5, 34, "0 1 2 3"),
  W("D2 E4 A2 V", 3, 10, 42, "0 x9 3 x9"),
  W("T2 D2 A2 V2", 3, 12, 46, "1 x8 2 x8"),
  W("E8 V3 A3 T2", 3, 13, 50, "0 1 2 3"),
  W("A4 D3 E8 V3 T", 3, 14, 54, "0 x19 3 x19"),
  W("T4 A5 V4 E8", 3, 15, 58, "0 x7 1 x7 2 x7 3 x7"),
  W("A6 E10 V4 T6", 3, 16, 64, "1 x13 2 x13"),
  W("D3 E8 V4 A5 T2", 3, 14, 96, "0 x22 2 x22 1 x22 3 x22", boss=(5, "B"), tail=("T6 E8 V5 A5", 3, 14)),
 ],
}
