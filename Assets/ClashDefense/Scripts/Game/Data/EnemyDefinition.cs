using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Un enemigo: sus números (núcleo) y cómo se ve. Editable en el Inspector.</summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Enemigo", fileName = "Enemigo_", order = 11)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Números del enemigo (Doc 04, Doc 05, GDS-002.5). 'code' es la letra que usan las oleadas (D, E, V, A, T, G, B).")]
        public EnemyTypeData data = new EnemyTypeData();

        [Header("Presentación")]
        [Tooltip("Color del cuerpo (UXS-002.5: medido también en grises).")]
        public Color color = Color.gray;
        [Tooltip("Cómo se ve en la partida.")]
        public EnemyVisual prefab;

        public string Id => data != null ? data.id : null;

        public EnemyTypeData ToCore() => DataCopy.Clone(data);
    }
}
