# Genera los datos w1-0.2 (Doc 05 v2.0) sobre la geometría existente. Idempotente.
import json, sys, os
D = sys.argv[1]
def load(f): return json.load(open(os.path.join(D, f), encoding='utf-8'))
def save(f, o, compact=False):
    with open(os.path.join(D, f), 'w', encoding='utf-8') as fh:
        json.dump(o, fh, ensure_ascii=False, indent=1 if compact else 2); fh.write('\n')

b = load('balance_w1.json')
b['version'] = 'w1-0.2'
b['source'] = ('Documento 05 v2.0 (28-09-2026), aprobado para implementación: costos, estadísticas N1/N2, enemigos, '
               'oro inicial y reglas de oleadas tal cual. Lo que el documento no numera (velocidad de proyectil, huella, '
               'tick de quemadura, textos) sigue de w1-0.1. Supuestos en GDS-004.2; cambios en CHANGELOG_balance.md.')
b['economy'] = {'startGold': 150, 'tutorialStartGold': 100, 'baseHp': 100, 'upgradeCostFactor': 1.0, 'sellRefundFactor': 0.6}
b['waveRules'] = {'order': 'D E V T A', 'minibossAfter': 0.75, 'minibossGap': 3.0}
T = {t['id']: t for t in b['towers']}
T['arqueras']['cost'] = 100
T['canon']['cost'] = 125
T['mago']['cost'] = 150; T['mago']['metalEfficiency'] = 0.5
T['mortero'].update(cost=180, levels=[
    {'damage': 180, 'interval': 4.5, 'range': 10.0, 'minRange': 3.0, 'areaRadius': 2.25, 'flightTime': 2.0},
    {'damage': 240, 'interval': 4.0, 'range': 11.0, 'minRange': 3.0, 'areaRadius': 2.6, 'flightTime': 1.8}])
T['bombardera'].update(cost=160, levels=[
    {'damage': 70, 'interval': 2.0, 'range': 6.5, 'areaRadius': 1.6},
    {'damage': 95, 'interval': 1.8, 'range': 7.0, 'areaRadius': 1.9}])
T['electrica'].update(cost=170, levels=[
    {'damage': 18, 'interval': 0.45, 'range': 4.5, 'chainJumps': 2, 'chainRadius': 3.0},
    {'damage': 24, 'interval': 0.40, 'range': 5.0, 'chainJumps': 3, 'chainRadius': 3.25}])
T['infernal'].update(cost=200, levels=[
    {'interval': 0.1, 'range': 6.5, 'rampDps': [20, 55, 120], 'rampTimes': [0, 2.0, 5.0]},
    {'interval': 0.1, 'range': 7.0, 'rampDps': [25, 70, 150], 'rampTimes': [0, 1.5, 4.0]}])
T['infernal']['description'] = 'Rayo que se queda con un objetivo y calienta en tres etapas. Tierra y aire. Solo en la etapa máxima rompe el metal.'
T['oro'].update(cost=175, levels=[
    {'goldPerCycle': 10, 'goldCycle': 8.0, 'goldCapacity': 50},
    {'goldPerCycle': 15, 'goldCycle': 6.0, 'goldCapacity': 90}])
T['oro']['description'] = 'No ataca: suma oro cada ciclo hasta llenarse. Clic en la torre para recogerlo.'
T['lanzallamas'].update(cost=190, levels=[
    {'damage': 12, 'pulseInterval': 0.2, 'burstDuration': 1.2, 'interval': 3.0, 'range': 6.5, 'flameWidth': 1.0, 'burnDps': 8, 'burnDuration': 4.0},
    {'damage': 16, 'pulseInterval': 0.2, 'burstDuration': 1.4, 'interval': 2.8, 'range': 7.0, 'flameWidth': 1.2, 'burnDps': 12, 'burnDuration': 5.0}])
T['lanzallamas']['description'] = 'Ráfaga de pulsos en línea que quema y deja fuego en el camino. Tierra y aire. No daña el metal.'
E = {e['id']: e for e in b['enemies']}
E['duende'].update(hp=100, armor=0, travelTime=30.0, baseDamage=10, gold=20)
E['esqueleto'].update(hp=60, armor=0, travelTime=24.0, baseDamage=5, gold=12)
E['esbirro'].update(hp=90, armor=0, travelTime=26.0, baseDamage=10, gold=20)
E['tanque'].update(hp=500, armor=0, travelTime=45.0, baseDamage=25, gold=50, heavy=True)
E['blindado'].update(hp=120, armor=240, travelTime=40.0, travelTimeExposed=30.0, baseDamage=20, gold=45)
E['gigante'].update(hp=3000, armor=0, travelTime=70.0, baseDamage=60, gold=160, heavy=True, miniboss=True)
E['dragon'].update(hp=2200, armor=0, travelTime=60.0, baseDamage=60, gold=160, miniboss=True)
E['duende']['displayName'] = 'Duende común'
E['blindado']['displayName'] = 'Duende con armadura'
save('balance_w1.json', b)

# Oleadas del Doc 05 v2.0 §10: G E V T A (C=0 en el Mundo 1), miniboss, intervalo. G del documento = D del balance.
W = {
 1: [(10,0,0,0,0,None,1.50),(14,0,0,0,0,None,1.40),(18,0,0,0,0,None,1.30),(22,0,0,0,0,None,1.20),(28,0,0,0,0,None,1.10)],
 2: [(10,0,0,0,0,None,1.40),(8,6,0,0,0,None,1.30),(10,10,0,0,0,None,1.20),(8,16,0,0,0,None,1.10),(14,14,0,0,0,None,1.00),(18,20,0,0,0,None,0.90)],
 3: [(12,6,0,0,0,None,1.30),(10,10,4,0,0,None,1.20),(12,12,6,0,0,None,1.10),(10,16,8,0,0,None,1.00),(14,14,10,0,0,None,0.90),(16,18,12,0,0,None,0.85),(18,20,14,0,0,'G',0.80)],
 4: [(12,8,0,0,0,None,1.30),(10,12,6,0,0,None,1.20),(8,8,0,2,0,None,1.10),(12,14,0,3,0,None,1.05),(10,10,8,3,0,None,1.00),(14,16,10,4,0,None,0.90),(16,18,12,5,0,None,0.85),(18,20,14,6,0,None,0.80)],
 5: [(12,10,4,0,0,None,1.25),(10,12,0,2,0,None,1.20),(12,14,8,3,0,None,1.10),(14,16,10,4,0,None,1.05),(10,20,12,4,0,None,1.00),(16,18,10,5,0,None,0.95),(18,20,12,6,0,None,0.90),(20,22,14,7,0,None,0.85),(22,24,16,8,0,None,0.80)],
 6: [(12,10,6,0,0,None,1.25),(10,12,0,2,0,None,1.20),(8,10,0,0,2,None,1.10),(12,14,8,0,3,None,1.05),(10,16,0,3,3,None,1.00),(14,18,10,4,4,None,0.95),(16,20,12,4,5,None,0.90),(18,22,14,5,6,None,0.85),(20,24,16,6,7,None,0.80),(22,26,18,6,8,'B',0.75)],
}
for n, waves in W.items():
    f = f'level_m1_n{n}.json'
    l = load(f)
    out = []
    for g, e, v, t, a, boss, iv in waves:
        comp = ' '.join(f'{c}{k}' for c, k in zip('DEVTA', (g, e, v, t, a)) if k > 0)
        w = {'composition': comp, 'spawnInterval': iv}
        if boss: w['miniboss'] = boss
        out.append(w)
    l['waves'] = out
    save(f, l, compact=True)

c = load('campaign_w1.json')
c['version'] = 'w1-0.2'
c['replayFactor'] = 0.0
c['bestResultOnly'] = True
c['starMultipliers'] = [0.5, 0.75, 1.0]
rewards = {'m1_n1': 100, 'm1_n2': 120, 'm1_n3': 140, 'm1_n4': 170, 'm1_n5': 200, 'm1_n6': 240, 'm2_n1': 260, 'm2_n2': 290, 'm2_n3': 320}
for w in c['worlds']:
    for cont in w['continents']:
        for lv in cont['levels']:
            if lv['id'] in rewards: lv['baseReward'] = rewards[lv['id']]
def mod(stat, op, value): return {'stat': stat, 'op': op, 'value': value}
c['shop'] = [
 {'id': 'mortero_espoleta', 'tower': 'mortero', 'displayName': 'Espoleta rápida', 'description': 'La bomba tarda 20 % menos en caer', 'cost': 80, 'mods': [mod('flightTime', 'mul', 0.8)]},
 {'id': 'bombardera_polvora', 'tower': 'bombardera', 'displayName': 'Pólvora extra', 'description': 'Radio de explosión +15 %', 'cost': 100, 'mods': [mod('areaRadius', 'mul', 1.15)]},
 {'id': 'electrica_bobina', 'tower': 'electrica', 'displayName': 'Bobina doble', 'description': 'La cadena alcanza un objetivo más', 'cost': 120, 'mods': [mod('chainJumps', 'add', 1)]},
 {'id': 'infernal_nucleo', 'tower': 'infernal', 'displayName': 'Núcleo rápido', 'description': 'Llega a cada etapa 15 % antes', 'cost': 140, 'mods': [mod('rampTimes', 'mul', 0.85)]},
 {'id': 'oro_veta', 'tower': 'oro', 'displayName': 'Veta rica', 'description': 'Ciclo de producción 15 % más corto', 'cost': 160, 'mods': [mod('goldCycle', 'mul', 0.85)]},
 {'id': 'lanzallamas_combustible', 'tower': 'lanzallamas', 'displayName': 'Combustible denso', 'description': 'Quemadura y fuego en el piso duran 1 s más', 'cost': 180, 'mods': [mod('burnDuration', 'add', 1.0)]},
]
c['tanda'] = {
 'description': 'Insignia de maestría: completar los tres niveles de un continente entrega una. Se asigna a Arqueras, Cañón o Mago (hasta dos por torre) y se puede redistribuir gratis desde el mapa.',
 'maxPerTower': 2, 'redistributable': True,
 'options': [
  {'tower': 'arqueras', 'description': '+8 % de daño y +5 % de velocidad de flecha por insignia', 'mods': [mod('damage', 'pct', 8), mod('projectileSpeed', 'pct', 5)]},
  {'tower': 'canon', 'description': '+10 % de daño por insignia', 'mods': [mod('damage', 'pct', 10)]},
  {'tower': 'mago', 'description': '+8 % de daño y +5 % de área por insignia', 'mods': [mod('damage', 'pct', 8), mod('areaRadius', 'pct', 5)]},
 ]}
save('campaign_w1.json', c)
print('ok')
