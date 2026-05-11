using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float thickness = 3f;

    private Image[] borderImages;

    void Awake()
    {
        // Disable the default color-tint highlight so the fill stays unchanged on hover
        var btn = GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = colors.normalColor;
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;

        CreateBorder();
    }

    void CreateBorder()
    {
        string[] names = { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" };
        borderImages = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject(names[i]);
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = outlineColor;
            img.raycastTarget = false;
            borderImages[i] = img;
            go.SetActive(false);
        }

        float t = thickness;
        // Top strip
        SetRect(borderImages[0].rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -t), Vector2.zero);
        // Bottom strip
        SetRect(borderImages[1].rectTransform, 0f, 0f, 1f, 0f, Vector2.zero, new Vector2(0, t));
        // Left strip
        SetRect(borderImages[2].rectTransform, 0f, 0f, 0f, 1f, Vector2.zero, new Vector2(t, 0));
        // Right strip
        SetRect(borderImages[3].rectTransform, 1f, 0f, 1f, 1f, new Vector2(-t, 0), Vector2.zero);
    }

    void SetRect(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (var img in borderImages)
            img.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        foreach (var img in borderImages)
            img.gameObject.SetActive(false);
    }
}
