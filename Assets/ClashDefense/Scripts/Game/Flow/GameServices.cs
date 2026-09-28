using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Las piezas compartidas del juego armado. Las crea GameBootstrap (raíz de composición) y las usan el flujo y la partida.</summary>
    public sealed class GameServices
    {
        public GameConfig Config;
        public Camera Camera;
        public CameraRig Rig;
        public Hud Hud;
        public Menus Menus;
        public WorldView World;
        public LevelStage Stage;
        public Sfx Sfx;
        public MetricsWriter Metrics;

        /// <summary>Encuadra un nivel: todo el mapa, todas las entradas y la base (Doc 03 §20).</summary>
        public void Fit(Core.LevelData level)
        {
            var b = WorldView.ContentBounds(level);
            if (Rig == null) Rig = new CameraRig(Camera, b); else Rig.SetContent(b);
        }
    }
}
