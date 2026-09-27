import json, os, matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'ClashDefense', 'Data')
fig, axes = plt.subplots(2, 3, figsize=(18, 7.5))
for i, ax in enumerate(axes.flat):
    lid = f"m1_n{i+1}"
    L = json.load(open(os.path.join(D, f"level_{lid}.json"), encoding='utf-8'))
    a = L['buildArea']
    ax.add_patch(plt.Rectangle((a['minX'], a['minZ']), a['maxX']-a['minX'], a['maxZ']-a['minZ'], color='#8FA878' if L['theme']=='praderas' else '#C9A27A'))
    cols = ['#5A4A36', '#7a2020', '#203a7a', '#207a3a']
    for k, r in enumerate(L['routes']):
        xs = [p['x'] for p in r['points']]; zs = [p['z'] for p in r['points']]
        ax.plot(xs, zs, color=cols[k % 4], lw=6, alpha=0.6, solid_capstyle='round')
        ax.plot(xs[0], zs[0], 's', color='k', ms=10)
    b = L['routes'][0]['points'][-1]
    ax.add_patch(plt.Circle((b['x'], b['z']), L['baseRadius'], color='#2E3440'))
    for c in L['blocked']:
        ax.add_patch(plt.Circle((c['x'], c['z']), c['radius'], color='#33363B'))
    h = L.get('tutorialHint')
    if h and h['radius'] > 0: ax.add_patch(plt.Circle((h['x'], h['z']), h['radius'], fill=False, color='gold', lw=2))
    ax.set_xlim(-22, 22); ax.set_ylim(-12.5, 12.5); ax.set_aspect('equal'); ax.set_title(L['displayName'])
    ax.grid(alpha=0.2)
plt.tight_layout(); plt.savefig(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mapas_mundo1.png'), dpi=70)
print('ok')
