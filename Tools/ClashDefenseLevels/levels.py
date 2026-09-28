# Generador de los niveles del Mundo 1 (LDS-002.6). Fue la fuente de geometría y oleadas hasta TL-003; ahora
# escribe propuestas/level_m1_nX.json. Desde TL-003 los niveles del juego se editan en sus escenas de Unity
# (Assets/ClashDefense/Scenes/Niveles): este generador quedó para bocetar niveles nuevos, no pisa los datos del juego.
import json, sys, os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'propuestas')
os.makedirs(OUT, exist_ok=True)
AREA = {"minX": -20.0, "minZ": -11.0, "maxX": 20.0, "maxZ": 11.0}

def pts(*p): return [{"x": float(x), "z": float(z)} for x, z in p]
def circ(x, z, r): return {"x": float(x), "z": float(z), "radius": float(r)}

LEVELS = {}

LEVELS["m1_n1"] = dict(
    name="El sendero", theme="praderas", tutorial="arqueras", hint=circ(4.5, 3.5, 1.6),
    routes=[pts((-21, 7), (13, 7), (13, 0), (-13, 0), (-13, -7), (18, -7))],
    blocked=[],
    waves=[])
LEVELS["m1_n2"] = dict(
    name="La colina", theme="praderas",
    routes=[pts((-15, 12), (-15, -5), (-6, -5), (-6, 6), (4, 6), (4, -5), (13, -5), (13, 5))],
    blocked=[circ(-10, -9, 1.6), circ(9, 9.2, 1.5), circ(17.5, -8.5, 1.4), circ(-18, 4, 1.3)],
    waves=[])
LEVELS["m1_n3"] = dict(
    name="El cruce", theme="praderas",
    routes=[pts((-21, 8), (-6, 8), (-6, 0), (6, 0), (6, 7), (15, 7), (15, -6), (8, -6)),
            pts((-21, -8), (-6, -8), (-6, 0), (6, 0), (6, 7), (15, 7), (15, -6), (8, -6))],
    blocked=[circ(-14, 0, 2.0), circ(0, 5.5, 1.6), circ(18.5, 9.5, 1.2), circ(1, -8, 1.5)],
    waves=[])
LEVELS["m1_n4"] = dict(
    name="La garganta", theme="desfiladero",
    routes=[pts((21, -8), (12, -8), (12, 0), (12, 7), (-4, 7), (-4, 0), (-14, 0), (-14, -7)),
            pts((21, -8), (12, -8), (12, 0), (4, 0), (4, -7), (-4, -7), (-4, 0), (-14, 0), (-14, -7))],
    blocked=[circ(17, 3, 2.0), circ(-12, 8, 1.5), circ(-18.5, -2.5, 1.2), circ(8, -9.8, 1.2), circ(17, -4.5, 2.0), circ(8, -3.8, 1.9)],
    waves=[])
LEVELS["m1_n5"] = dict(
    name="Dos puertas", theme="desfiladero",
    routes=[pts((-21, 0), (-12, 0), (-12, 9), (-2, 9), (-2, 4), (8, 4), (8, 9), (14, 9), (14, 0)),
            pts((-21, 0), (-12, 0), (-12, -9), (-2, -9), (-2, -4), (8, -4), (8, -9), (14, -9), (14, 0))],
    blocked=[circ(-16.5, 3.4, 2.2), circ(-16.5, -3.4, 2.2), circ(18, 6, 1.2), circ(18, -6, 1.2)],
    waves=[])
LEVELS["m1_n6"] = dict(
    name="El paso del dragón", theme="desfiladero",
    routes=[pts((-21, 9), (-12, 9), (-12, 0), (-4, 0), (-4, 6), (8, 6), (8, 0), (17, 0)),
            pts((-21, 9), (-12, 9), (-12, 0), (-4, 0), (-4, -6), (8, -6), (8, 0), (17, 0)),
            pts((-21, -9), (-12, -9), (-12, 0), (-4, 0), (-4, 6), (8, 6), (8, 0), (17, 0)),
            pts((-21, -9), (-12, -9), (-12, 0), (-4, 0), (-4, -6), (8, -6), (8, 0), (17, 0))],
    blocked=[circ(2, 0, 1.6), circ(-17, 0, 2.0), circ(13, 8, 1.5), circ(13, -8, 1.5), circ(-8, 3.4, 2.1), circ(-8, -3.4, 2.1)],
    waves=[])

def load_waves():
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'waves.json')
    return json.load(open(p, encoding='utf-8')) if os.path.exists(p) else {}

def build():
    waves = load_waves()
    for lid, d in LEVELS.items():
        n = int(lid[-1])
        lvl = {
            "id": lid,
            "displayName": f"Mundo 1 · Nivel {n} — {d['name']}",
            "theme": d["theme"],
            "routes": [{"points": r} for r in d["routes"]],
            "pathWidth": 2.2,
            "buildArea": AREA,
            "blocked": d["blocked"],
            "baseRadius": 1.6,
            "tutorialTowerId": d.get("tutorial", ""),
            "tutorialHint": d.get("hint", circ(0, 0, 0)),
            "waves": waves.get(lid, []),
        }
        with open(os.path.join(OUT, f"level_{lid}.json"), 'w', encoding='utf-8') as f:
            json.dump(lvl, f, ensure_ascii=False, indent=1)
    print("ok", len(LEVELS))

if __name__ == '__main__':
    build()
