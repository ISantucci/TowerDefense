using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Ayudas para construir la interfaz de UXS-001.6 en código (SOL-001 D6).</summary>
    public static class UiKit
    {
        public static Canvas Canvas(Transform parent, int order)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Panel(RectTransform rt, Color color, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                                           Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, sizeDelta);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Una sola línea que se achica hasta minSize antes de desbordar (tarjetas del HUD).</summary>
        public static void OneLine(TextMeshProUGUI t, float minSize)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = minSize;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        public static TextMeshProUGUI Fill(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
            => Text(parent, name, text, size, color, align, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        public static Button Button(Transform parent, string name, string label, float fontSize, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, out TextMeshProUGUI text)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var img = Panel(rt, Palette.PanelClaro);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            colors.fadeDuration = 0.05f;
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            text = Fill(rt, "Texto", label, fontSize, Palette.Texto, TextAlignmentOptions.Center);
            text.margin = new Vector4(8, 4, 8, 4);
            return b;
        }

        public static Outline Outline(GameObject go, Color c, float width)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(width, -width);
            return o;
        }

        /// <summary>Borde de cuatro barras: se ve sobre fondos transparentes y no tiñe el panel (el Outline de uGUI sí).</summary>
        public static RectTransform Frame(RectTransform rt, Color c, float th)
        {
            var f = Stretch("Borde", rt);
            Edge(f, "Arriba", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, th), c);
            Edge(f, "Abajo", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th), c);
            Edge(f, "Izquierda", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0), c);
            Edge(f, "Derecha", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(th, 0), c);
            return f;
        }

        static void Edge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color c)
        {
            var e = Rect(name, parent, aMin, aMax, pivot, Vector2.zero, size);
            Panel(e, c, false);
        }

        /// <summary>Casilla legible sin sprites: caja oscura con borde claro y marca de acento (UXS-001.6).</summary>
        public static void StyleToggle(GameObject toggleGo, float box = 30f)
        {
            var toggle = toggleGo.GetComponent<Toggle>();
            if (toggle == null) return;
            var bg = toggle.targetGraphic as Image;
            if (bg != null)
            {
                var brt = bg.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
                brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(box, box);
                bg.sprite = null; bg.color = Palette.PanelClaro;
                Frame(brt, Palette.Texto2, 2f);
            }
            var mark = toggle.graphic as Image;
            if (mark != null)
            {
                var mrt = mark.rectTransform;
                mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.offsetMin = new Vector2(7, 7); mrt.offsetMax = new Vector2(-7, -7);
                mark.sprite = null; mark.color = Palette.Acento;
                mark.transform.SetAsLastSibling();
            }
        }

        static Sprite star;
        /// <summary>Estrella dibujada por código (sin depender de un glifo de la fuente).</summary>
        public static Sprite Star()
        {
            if (star != null) return star;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? 0.48f : 0.2f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    bool inside = false;
                    for (int i = 0, j = 9; i < 10; j = i++)
                        if (((pts[i].y > p.y) != (pts[j].y > p.y)) && (p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)) inside = !inside;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
                }
            tex.Apply();
            star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return star;
        }

        /// <summary>Número con coma decimal (español).</summary>
        public static string Num(float v, string fmt = "0.##") => v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>Avisa cuando el mouse entra y sale de un elemento (preview de la mejora, UXS-001.3).</summary>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Enter, Exit;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        void OnDisable() => Exit?.Invoke();
    }
}
