using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Qué juego arma una escena de arranque: la campaña (con su balance y sus niveles) y el Laboratorio (el P0).
    /// ClashDefense.unity usa el de la campaña; Prototipo0.unity, uno sin campaña. Es el único asset que GameBootstrap necesita.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Configuración del juego", fileName = "Juego_", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Tooltip("Versión que viaja en el registro de cada partida (MET-002.7) y en el pie de la pantalla inicial.")]
        public string version = "w1-0.1";
        [Tooltip("Vacío = el Prototipo 0 solo, como se entregó en TL-001.")]
        public CampaignDefinition campaign;
        [Header("Laboratorio (Prototipo 0)")]
        public BalanceDefinition labBalance;
        public LevelDefinition labLevel;
        [Header("Presentación")]
        public PresentationLibrary presentation;

        public bool IsCampaign => campaign != null;
    }
}
