using UnityEngine;
using UnityEngine.UI;

/// <summary>Tarjeta de un nivel en el selector: nombre, subtítulo, estrellas y candado. Todo cableado en la escena.</summary>
public class LevelSelectButton : MonoBehaviour
{
    public Button button;
    public Image background;
    public Image familyStrip;
    public Text numberText;
    public Text nameText;
    public Text subtitleText;
    public Text starsText;
    public GameObject lockOverlay;

    LevelDefinition level;

    public void Bind(LevelDefinition def, int number)
    {
        level = def;
        bool has = def != null;
        gameObject.SetActive(true);

        if (numberText != null) numberText.text = "NIVEL " + number;
        if (nameText != null) nameText.text = has ? def.displayName : "—";
        if (subtitleText != null) subtitleText.text = has ? def.subtitle : "";
        if (familyStrip != null) familyStrip.color = has ? DefensePalette.Family(def.family) : Color.gray;

        bool unlocked = has && LevelCatalog.IsUnlocked(def);
        int stars = has ? LevelCatalog.Stars(def) : 0;
        if (starsText != null)
        {
            string s = "";
            for (int i = 0; i < 3; i++) s += i < stars ? "\u2605 " : "\u2606 ";
            starsText.text = has && LevelCatalog.IsWon(def) ? s.TrimEnd() : (unlocked ? "sin jugar" : "bloqueado");
        }
        if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
        if (button != null)
        {
            button.interactable = unlocked;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Play);
        }
    }

    void Play()
    {
        if (level == null) return;
        GameFlow.StartLevel(level);
    }
}
