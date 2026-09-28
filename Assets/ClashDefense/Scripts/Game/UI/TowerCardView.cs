using ClashDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Tarjeta de una torre en la franja inferior del HUD (UXS-002.4). Va en el prefab TarjetaTorre; el HUD las recicla.</summary>
    [AddComponentMenu("Clash Defense/UI/Tarjeta de torre")]
    public sealed class TowerCardView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image stripe;
        [SerializeField] internal Outline selection;
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal TextMeshProUGUI keyText, nameText, costText, targetsText;
        [SerializeField] internal HoverRelay hover;
        [Tooltip("Ancho por debajo del cual la tarjeta usa textos cortos.")]
        [SerializeField] internal float compactWidth = 230f;

        public string Id { get; private set; }
        public int Cost { get; private set; }
        public TowerTypeData Type { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public bool Compact { get; private set; }
        int lastAffordGold = int.MinValue;

        public void Bind(TowerTypeData t, int index, Color family, float width, float x)
        {
            Id = t.id; Cost = t.cost; Type = t;
            name = "Tarjeta_" + t.id;
            var rt = Rect;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(x, 0f);
            Compact = width < compactWidth;
            stripe.color = family;
            keyText.text = $"[{Hud.KeyLabel(index)}]";
            keyText.fontSize = Compact ? 19 : 24;
            nameText.text = t.Short;
            nameText.fontSizeMax = Compact ? 22 : 28;
            costText.fontSizeMax = Compact ? 21 : 26;
            targetsText.text = Compact ? Hud.TargetsShort(t) : Hud.TargetsLine(t);
            targetsText.fontSizeMax = Compact ? 16 : 20;
            selection.enabled = false;
            lastAffordGold = int.MinValue;
        }

        /// <summary>Precio y lo que falta (UXS-001.2): solo cambia el texto si cambió el oro.</summary>
        public void SetGold(int gold, Color affordColor, Color missingColor)
        {
            if (gold == lastAffordGold) return;
            lastAffordGold = gold;
            bool afford = gold >= Cost;
            costText.text = afford ? $"{Cost} oro" : Compact ? $"{Cost} · faltan {Cost - gold}" : $"{Cost} oro · faltan {Cost - gold}";
            costText.color = afford ? affordColor : missingColor;
        }

        public void SetInteractable(bool ok)
        {
            button.interactable = ok;
            group.alpha = ok ? 1f : 0.45f;
        }

        public void SetSelected(bool on) { if (selection.enabled != on) selection.enabled = on; }
    }
}
