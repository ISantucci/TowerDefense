using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Paleta medida de UXS-001.1 … UXS-001.6 y UXS-002.1 … UXS-002.5 (legibilidad.py en ley). Presentación, no balance.</summary>
    public static class Palette
    {
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // mundo (P0 y continente Praderas)
        public static readonly Color Terreno = Hex("#8FA878");
        public static readonly Color FueraDelArea = Hex("#6F8660");
        public static readonly Color Camino = Hex("#5A4A36");
        public static readonly Color Base = Hex("#2E3440");
        public static readonly Color BaseAlmena = Hex("#434C5E");
        public static readonly Color Entrada = Hex("#2A2A2E");
        public static readonly Color Bloqueado = Hex("#33363B");
        public static readonly Color Zocalo = Hex("#1B1F26");
        public static readonly Color Banda = Hex("#E8E2D0");
        // continente Desfiladero (UXS-002.2)
        public static readonly Color TerrenoDesfiladero = Hex("#C9A27A");
        public static readonly Color FueraDesfiladero = Hex("#9C7753");
        public static readonly Color CaminoDesfiladero = Hex("#5E3F2E");
        public static readonly Color RocaDesfiladero = Hex("#4A3A33");
        public static readonly Color RocaPraderas = Hex("#4F5B45");

        // torres
        public static readonly Color Arqueras = Hex("#63C5F2");
        public static readonly Color Canon = Hex("#D9822B");
        public static readonly Color Mago = Hex("#5B2A9E");
        public static readonly Color Mortero = Hex("#6E7F3A");
        public static readonly Color Bombardera = Hex("#C23B3B");
        public static readonly Color Electrica = Hex("#F5E663");
        public static readonly Color Infernal = Hex("#E0457B");
        public static readonly Color OroTorre = Hex("#F2C14E");
        public static readonly Color Lanzallamas = Hex("#FF6F3C");
        public static readonly Color Orbe = Hex("#B57BFF");
        public static readonly Color Flecha = Hex("#F2E3A0");
        public static readonly Color Bala = Hex("#2B2B2B");
        public static readonly Color Rayo = Hex("#FFF6A8");
        public static readonly Color Fuego = Hex("#FF8A1F");

        // enemigos
        public static readonly Color Duende = Hex("#86D25A");
        public static readonly Color Esqueleto = Hex("#F4F1E8");
        public static readonly Color Esbirro = Hex("#2E5BD6");
        public static readonly Color Metal = Hex("#9AA3AD");
        public static readonly Color Tanque = Hex("#6E9A3A");    // UXS-002.5: se distingue del Duende, el Esqueleto y el Esbirro también en grises
        public static readonly Color TanquePlaca = Hex("#D9CBA3");   // la placa clara despega al tanque del camino oscuro (UXS-002.5: 1,6:1 sin ella)
        public static readonly Color Gigante = Hex("#66B040");   // UXS-002.5: 3,2:1 sobre el camino de Praderas
        public static readonly Color Dragon = Hex("#E05A3A");    // UXS-002.5: el rojo anterior colapsaba con el Esbirro en grises (1,06:1)
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
        public static readonly Color Cristal = Hex("#8FD3FF");

        public static Color ForTower(string id)
        {
            switch (id)
            {
                case "arqueras": return Arqueras;
                case "canon": return Canon;
                case "mago": return Mago;
                case "mortero": return Mortero;
                case "bombardera": return Bombardera;
                case "electrica": return Electrica;
                case "infernal": return Infernal;
                case "oro": return OroTorre;
                case "lanzallamas": return Lanzallamas;
                default: return Bloqueo;
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
                case "tanque": return Tanque;
                case "gigante": return Gigante;
                case "dragon": return Dragon;
                default: return Color.gray;
            }
        }

        /// <summary>Terreno, afuera, camino y rocas por continente (UXS-002.2: cada continente con rasgos propios).</summary>
        public static void Theme(string theme, out Color terrain, out Color outside, out Color path, out Color rock)
        {
            switch (theme)
            {
                case "desfiladero": terrain = TerrenoDesfiladero; outside = FueraDesfiladero; path = CaminoDesfiladero; rock = RocaDesfiladero; break;
                case "praderas": terrain = Terreno; outside = FueraDelArea; path = Camino; rock = RocaPraderas; break;
                default: terrain = Terreno; outside = FueraDelArea; path = Camino; rock = Bloqueado; break;
            }
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
