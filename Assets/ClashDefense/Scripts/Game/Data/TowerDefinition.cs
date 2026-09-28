using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Una torre: sus reglas y números (los usa el núcleo tal cual) y cómo se ve. Es un asset editable en el Inspector:
    /// cambiar un número acá cambia el juego, sin tocar código. El validador corre al guardar y avisa arriba del Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Torre", fileName = "Torre_", order = 10)]
    public sealed class TowerDefinition : ScriptableObject
    {
        [Tooltip("Reglas y números de la torre (Doc 04, Doc 05, GDS-002.4). Los niveles son N1 y N2; el N3 no existe en la simulación.")]
        public TowerTypeData data = new TowerTypeData();

        [Header("Presentación")]
        [Tooltip("Color de la familia: la franja de la tarjeta, la tienda y el cuerpo de la torre.")]
        public Color color = Color.gray;
        [Tooltip("Cómo se ve en la partida.")]
        public TowerVisual prefab;
        [Tooltip("Proyectil que dispara. Vacío = no dispara proyectil (eléctrica, infernal, lanzallamas, oro).")]
        public ProjectileVisual projectile;

        public string Id => data != null ? data.id : null;

        /// <summary>Copia para una partida: el asset no cambia aunque la partida lo modifique.</summary>
        public TowerTypeData ToCore() => DataCopy.Clone(data);
    }
}
