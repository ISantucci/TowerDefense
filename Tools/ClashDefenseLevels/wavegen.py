# Oleadas del Mundo 1 (LDS-002.6) escritas como receta: una ráfaga (lista de códigos) × repeticiones, con lugares
# vacíos entre ráfagas, duración de la aparición (span) y reparto entre recorridos. El intervalo = span / lugares.
import json, os, sys

def rle(codes):
    out = []; i = 0
    while i < len(codes):
        j = i
        while j < len(codes) and codes[j] == codes[i]: j += 1
        n = j - i
        out.append(codes[i] if n == 1 else f"{codes[i]} x{n}")
        i = j
    return ' '.join(out)

def B(s):
    """'D3 E2 V' -> ['D','D','D','E','E','V']"""
    out = []
    for tok in s.split():
        c = tok[0]; n = int(tok[1:]) if len(tok) > 1 else 1
        out += [c] * n
    return out

def W(burst, reps, gap, span, lanes=None, boss=None, tail=None):
    return dict(burst=B(burst), reps=reps, gap=gap, span=span, lanes=lanes, boss=boss, tail=tail)

def seq(w):
    burst = w['burst']; g = w['gap']
    inner = rle(burst) + ((' .' + (f' x{g}' if g > 1 else '')) if g else '')
    parts = [f"({inner})x{w['reps']}"]
    slots = w['reps'] * (len(burst) + g)
    count = w['reps'] * len(burst)
    if w['boss']:
        lead, code = w['boss']
        parts.append(('. x%d ' % lead if lead > 1 else '. ' if lead == 1 else '') + code); slots += lead + 1; count += 1
    if w['tail']:
        tb, tr, tg = w['tail']
        tb = B(tb)
        parts.append(f"({rle(tb)}{(' .' + (f' x{tg}' if tg > 1 else '')) if tg else ''})x{tr}"); slots += tr * (len(tb) + tg); count += tr * len(tb)
    d = {"sequence": ' / '.join(parts), "spawnInterval": round(w['span'] / slots, 3)}
    if w['lanes']: d["lanes"] = w['lanes']
    return d, count

if __name__ == '__main__':
    from spec import SPEC
    here = os.path.dirname(os.path.abspath(__file__))
    out = json.load(open(os.path.join(here, 'waves.json')))
    for lid, ws in SPEC.items():
        out[lid] = []
        tot = 0
        for w in ws:
            d, c = seq(w); out[lid].append(d); tot += c
        print(lid, len(ws), 'oleadas', tot, 'enemigos')
    json.dump(out, open(os.path.join(here, 'waves.json'), 'w'), indent=1, ensure_ascii=False)
