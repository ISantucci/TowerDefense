using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Helpers compartidos por toda la UI creada en runtime (uGUI, sin prefabs, sin TMP).
/// Un solo look: paneles oscuros translúcidos, texto blanco, dorado para el oro, acentos por familia.
/// </summary>
public static class UiKit
{
    // ───────────────────────── paleta ─────────────────────────

    public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color PanelStrong = new Color(0.05f, 0.05f, 0.08f, 0.88f);
    public static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.16f);
    public static readonly Color TextColor = Color.white;
    public static readonly Color MutedText = new Color(0.78f, 0.80f, 0.86f, 1f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);
    public static readonly Color Danger = new Color(1f, 0.38f, 0.32f, 1f);
    public static readonly Color Good = new Color(0.35f, 1f, 0.45f, 1f);
    public static readonly Color ButtonColor = new Color(0.17f, 0.19f, 0.26f, 0.96f);
    public static readonly Color ButtonAccent = new Color(0.20f, 0.46f, 0.86f, 0.96f);
    public static readonly Color CardColor = new Color(0.10f, 0.11f, 0.16f, 0.94f);

    // anclas frecuentes
    public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    public static readonly Vector2 TopRight = new Vector2(1f, 1f);
    public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);
    public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

    // ───────────────────────── fuente ─────────────────────────

    static Font cachedFont;

    /// <summary>Fuente legacy: la de cualquier Text de la escena, o LegacyRuntime.ttf, o una del sistema.</summary>
    public static Font DefaultFont
    {
        get
        {
            if (cachedFont != null) return cachedFont;

            var hud = UnityEngine.Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (hud != null)
            {
                if (hud.txtMoney != null && hud.txtMoney.font != null) cachedFont = hud.txtMoney.font;
                else if (hud.txtLives != null && hud.txtLives.font != null) cachedFont = hud.txtLives.font;
            }
            if (cachedFont == null)
            {
                var anyText = UnityEngine.Object.FindFirstObjectByType<Text>(FindObjectsInactive.Include);
                if (anyText != null && anyText.font != null) cachedFont = anyText.font;
            }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (System.Exception) { cachedFont = null; }
            }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (System.Exception) { cachedFont = null; }
            }
            if (cachedFont == null)
            {
                try { cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16); }
                catch (System.Exception) { cachedFont = null; }
            }
            return cachedFont;
        }
    }

    // ───────────────────────── escena ─────────────────────────

    /// <summary>Canvas de la escena: primero por nombre, si no el primer Canvas raíz habilitado.</summary>
    public static Canvas FindCanvas(string preferredName)
    {
        if (!string.IsNullOrEmpty(preferredName))
        {
            var go = GameObject.Find(preferredName);
            if (go != null)
            {
                var c = go.GetComponent<Canvas>();
                if (c != null) return c.rootCanvas;
            }
        }

        var all = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas best = null;
        foreach (var c in all)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            var root = c.rootCanvas;
            if (root == null || !root.isActiveAndEnabled) continue;
            if (best == null || root.transform.GetSiblingIndex() < best.transform.GetSiblingIndex()) best = root;
        }
        return best;
    }

    /// <summary>Busca (recursivo, incluyendo inactivos) un hijo por nombre.</summary>
    public static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
            if (t != null && t != root && t.name == name) return t;
        return null;
    }

    /// <summary>Desactiva un hijo por nombre. Devuelve true si lo encontró.</summary>
    public static bool DeactivateChild(Transform root, string name)
    {
        var t = FindChildByName(root, name);
        if (t == null) return false;
        t.gameObject.SetActive(false);
        return true;
    }

    // ───────────────────────── creación básica ─────────────────────────

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
        if (layer < 0 || layer > 31) layer = 0;
        go.layer = layer;
        var rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = Center;
        rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 100f);
        return rt;
    }

    public static Image CreateImage(string name, Transform parent, Color color)
    {
        return CreateImage(name, parent, color, false);
    }

    public static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        var rt = CreateRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    /// <summary>Panel oscuro translúcido con borde de 2 px. Bloquea clicks (raycastTarget) por defecto.</summary>
    public static Image CreatePanel(string name, Transform parent, Color color, float outlinePx)
    {
        return CreatePanel(name, parent, color, outlinePx, true);
    }

    public static Image CreatePanel(string name, Transform parent, Color color, float outlinePx, bool raycastTarget)
    {
        var img = CreateImage(name, parent, color, raycastTarget);
        if (outlinePx > 0f) AddOutline(img.gameObject, OutlineColor, outlinePx);
        return img;
    }

    public static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
    {
        return CreateText(name, parent, content, size, color, anchor, FontStyle.Normal);
    }

    public static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        var rt = CreateRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = DefaultFont;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.text = content ?? string.Empty;
        return t;
    }

    /// <summary>Texto que se achica solo hasta entrar en su rect (una o varias líneas).</summary>
    public static void BestFit(Text t, int minSize, int maxSize)
    {
        if (t == null) return;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = minSize;
        t.resizeTextMaxSize = maxSize;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    public static Button CreateButton(string name, Transform parent, string label, UnityAction onClick)
    {
        return CreateButton(name, parent, label, onClick, ButtonColor, 16, new Vector2(160f, 36f));
    }

    public static Button CreateButton(string name, Transform parent, string label, UnityAction onClick, Color background, int fontSize, Vector2 size)
    {
        var img = CreateImage(name, parent, Color.white, true);
        img.rectTransform.sizeDelta = size;
        AddOutline(img.gameObject, OutlineColor, 1.5f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        SetHoverColors(btn, background);
        if (onClick != null) btn.onClick.AddListener(onClick);

        var txt = CreateText("Label", img.transform, label, fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(txt.rectTransform, 8f, 2f, 8f, 2f);
        BestFit(txt, 9, fontSize);
        return btn;
    }

    /// <summary>Texto (Label) de un botón creado con CreateButton.</summary>
    public static Text ButtonLabel(Button b)
    {
        if (b == null) return null;
        var t = b.transform.Find("Label");
        return t != null ? t.GetComponent<Text>() : null;
    }

    public static Toggle CreateToggle(string name, Transform parent, string label, bool isOn, UnityAction<bool> onChanged, int fontSize)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = new Vector2(320f, 30f);
        var toggle = rt.gameObject.AddComponent<Toggle>();

        var box = CreateImage("Background", rt, Color.white, true);
        Place(box.rectTransform, MiddleLeft, MiddleLeft, new Vector2(2f, 0f), new Vector2(24f, 24f));
        AddOutline(box.gameObject, new Color(1f, 1f, 1f, 0.35f), 1.5f);

        var check = CreateImage("Checkmark", box.transform, Gold, false);
        Place(check.rectTransform, Center, Center, Vector2.zero, new Vector2(14f, 14f));

        var txt = CreateText("Label", rt, label, fontSize, TextColor, TextAnchor.MiddleLeft);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.pivot = Center;
        txt.rectTransform.offsetMin = new Vector2(34f, 0f);
        txt.rectTransform.offsetMax = Vector2.zero;
        txt.raycastTarget = true;

        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.transition = Selectable.Transition.ColorTint;
        SetHoverColors(toggle, new Color(0.12f, 0.13f, 0.18f, 0.96f));
        toggle.isOn = isOn;
        if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
        return toggle;
    }

    public static Slider CreateSlider(string name, Transform parent, float value, UnityAction<float> onChanged)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = new Vector2(200f, 24f);
        var slider = rt.gameObject.AddComponent<Slider>();

        var bg = CreateImage("Background", rt, new Color(1f, 1f, 1f, 0.14f), true);
        Stretch(bg.rectTransform, 0f, 7f, 0f, 7f);

        var fillArea = CreateRect("Fill Area", rt);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.pivot = Center;
        fillArea.offsetMin = new Vector2(0f, 7f);
        fillArea.offsetMax = new Vector2(0f, -7f);

        var fill = CreateImage("Fill", fillArea, new Color(Gold.r, Gold.g, Gold.b, 0.9f), false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = Center;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        var handleArea = CreateRect("Handle Slide Area", rt);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.pivot = Center;
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);

        var handle = CreateImage("Handle", handleArea, Color.white, true);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.pivot = Center;
        handle.rectTransform.sizeDelta = new Vector2(16f, 0f);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        AddOutline(handle.gameObject, new Color(0f, 0f, 0f, 0.5f), 1f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.ColorTint;
        SetHoverColors(slider, new Color(0.92f, 0.92f, 0.95f, 1f));
        slider.value = Mathf.Clamp01(value);
        if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    public static CanvasGroup Group(GameObject go)
    {
        if (go == null) return null;
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    public static LayoutElement Layout(GameObject go, float preferredWidth, float preferredHeight)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (preferredWidth > 0f) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
        if (preferredHeight > 0f) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
        return le;
    }

    public static Outline AddOutline(GameObject go, Color color, float px)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(px, px);
        o.useGraphicAlpha = true;
        return o;
    }

    public static Shadow AddShadow(GameObject go, Color color, Vector2 distance)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = color;
        s.effectDistance = distance;
        s.useGraphicAlpha = true;
        return s;
    }

    // ───────────────────────── anclas ─────────────────────────

    /// <summary>Ancla puntual: anchorMin = anchorMax = anchor; posición y tamaño en unidades del canvas.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rt == null) return null;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    /// <summary>Estira al padre dejando márgenes.</summary>
    public static RectTransform Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        if (rt == null) return null;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Center;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    /// <summary>Estira horizontalmente a una altura fija medida desde el borde superior del padre.</summary>
    public static RectTransform StretchTop(RectTransform rt, float top, float height, float left, float right)
    {
        if (rt == null) return null;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static void SetFullscreen(RectTransform rt)
    {
        Stretch(rt, 0f, 0f, 0f, 0f);
    }

    // ───────────────────────── colores ─────────────────────────

    public static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    public static Color Brighten(Color c, float k)
    {
        return new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a);
    }

    public static Color Darken(Color c, float k)
    {
        return new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a);
    }

    /// <summary>Tinte de hover/pressed para cualquier Selectable (Button, Toggle, Slider).</summary>
    public static void SetHoverColors(Selectable s, Color normal)
    {
        if (s == null) return;
        var cb = s.colors;
        cb.normalColor = normal;
        cb.highlightedColor = Brighten(normal, 0.22f);
        cb.pressedColor = Darken(normal, 0.28f);
        cb.selectedColor = normal;
        cb.disabledColor = new Color(normal.r * 0.55f, normal.g * 0.55f, normal.b * 0.55f, normal.a * 0.6f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        s.colors = cb;
    }

    // ───────────────────────── sprites procedurales ─────────────────────────

    static Sprite starSprite;

    /// <summary>Estrella de 5 puntas blanca (32x32) para tintar con Image.color.</summary>
    public static Sprite StarSprite()
    {
        if (starSprite != null) return starSprite;

        const int S = 32;
        var vx = new float[10];
        var vy = new float[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = (i % 2 == 0) ? 15f : 6.5f;
            vx[i] = 16f + Mathf.Cos(a) * r;
            vy[i] = 16f + Mathf.Sin(a) * r;
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "UiKit_Star";
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                        if (PointInPolygon(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f, vx, vy)) cover += 1f / 9f;
                px[y * S + x] = new Color(1f, 1f, 1f, cover);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        starSprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 32f);
        starSprite.name = "UiKit_Star";
        starSprite.hideFlags = HideFlags.HideAndDontSave;
        return starSprite;
    }

    static bool PointInPolygon(float px, float py, float[] vx, float[] vy)
    {
        bool inside = false;
        int n = vx.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            bool cross = (vy[i] > py) != (vy[j] > py);
            if (cross && px < (vx[j] - vx[i]) * (py - vy[i]) / (vy[j] - vy[i]) + vx[i])
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Fila de 3 estrellas (llenas/vacías) dentro de un contenedor con layout horizontal.</summary>
    public static RectTransform CreateStars(string name, Transform parent, int stars, float size)
    {
        var row = CreateRect(name, parent);
        row.sizeDelta = new Vector2(size * 3f + 12f, size);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        for (int i = 0; i < 3; i++)
        {
            var img = CreateImage("Star" + i, row, i < stars ? Gold : new Color(1f, 1f, 1f, 0.2f), false);
            img.sprite = StarSprite();
            img.preserveAspect = true;
            Layout(img.gameObject, size, size);
        }
        return row;
    }

    /// <summary>Formatea velocidades: 1 → "1", 1.5 → "1.5".</summary>
    public static string FormatNumber(float v)
    {
        return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
    }

    // ───────────────────────── EventSystem ─────────────────────────

    /// <summary>True si el foco está en un InputField (para no robar teclas como Espacio o números).</summary>
    public static bool IsTyping()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        var go = es.currentSelectedGameObject;
        if (go == null) return false;
        var field = go.GetComponent<InputField>();
        if (field != null && field.isFocused) return true;
        return false;
    }

    /// <summary>Saca el foco de UI para que Espacio/Enter no vuelvan a "clickear" el último botón.</summary>
    public static void ClearSelection()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
    }

    /// <summary>True si el puntero está sobre algún elemento de UI con raycast.</summary>
    public static bool PointerOverUI()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }
}
