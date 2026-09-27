using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Paleta medida de UXS-001.1 … UXS-001.6 (legibilidad.py en ley). Presentación, no balance.</summary>
    public static class Palette
    {
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // mundo
        public static readonly Color Terreno = Hex("#8FA878");
        public static readonly Color FueraDelArea = Hex("#6F8660");
        public static readonly Color Camino = Hex("#5A4A36");
        public static readonly Color Base = Hex("#2E3440");
        public static readonly Color BaseAlmena = Hex("#434C5E");
        public static readonly Color Entrada = Hex("#2A2A2E");
        public static readonly Color Bloqueado = Hex("#33363B");
        public static readonly Color Zocalo = Hex("#1B1F26");
        public static readonly Color Banda = Hex("#E8E2D0");

        // torres
        public static readonly Color Arqueras = Hex("#63C5F2");
        public static readonly Color Canon = Hex("#D9822B");
        public static readonly Color Mago = Hex("#5B2A9E");
        public static readonly Color Orbe = Hex("#B57BFF");
        public static readonly Color Flecha = Hex("#F2E3A0");
        public static readonly Color Bala = Hex("#2B2B2B");

        // enemigos
        public static readonly Color Duende = Hex("#86D25A");
        public static readonly Color Esqueleto = Hex("#F4F1E8");
        public static readonly Color Esbirro = Hex("#2E5BD6");
        public static readonly Color Metal = Hex("#9AA3AD");
        public static readonly Color Sombra = new Color(0f, 0f, 0f, 0.35f);

        // señales
        public static readonly Color Valido = Hex("#FFFFFF");
        public static readonly Color Invalido = Hex("#FF6B6B");
        public static readonly Color Inmune = Hex("#FFFFFF");

        // interfaz
        public static readonly Color Panel = Hex("#1B1F26");
        public static readonly Color PanelClaro = Hex("#2A303B");
        public static readonly Color Texto = Hex("#F2F2F2");
        public static readonly Color Texto2 = Hex("#A9B0BB");
        public static readonly Color Oro = Hex("#F2C14E");
        public static readonly Color Vida = Hex("#FF5A5F");
        public static readonly Color Acento = Hex("#45D0C4");
        public static readonly Color Bloqueo = Hex("#8A929E");

        public static Color ForTower(string id)
        {
            switch (id)
            {
                case "arqueras": return Arqueras;
                case "canon": return Canon;
                case "mago": return Mago;
                default: return Color.gray;
            }
        }

        public static Color ForEnemy(string id)
        {
            switch (id)
            {
                case "duende": return Duende;
                case "esqueleto": return Esqueleto;
                case "esbirro": return Esbirro;
                case "blindado": return Metal;
                default: return Color.gray;
            }
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
