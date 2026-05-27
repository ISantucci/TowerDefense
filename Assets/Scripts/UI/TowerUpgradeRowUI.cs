using UnityEngine;
using UnityEngine.UI;

public class TowerUpgradeRowUI : MonoBehaviour
{
    [Header("Stat Icon  (circulo rojo provisorio)")]
    [SerializeField] Image statIcon;
    public Color statIconColor = Color.red;
    public Sprite statIconSprite;   // asignar cuando haya sprites reales

    [Header("Level Blocks  (uno por maximo nivel)")]
    [SerializeField] Image[] levelBlocks;
    public Color filledColor  = new Color(0.20f, 0.80f, 0.20f);
    public Color emptyColor   = new Color(0.25f, 0.25f, 0.25f);
    public Sprite levelBlockSprite;   // asignar cuando haya sprites reales

    [Header("Upgrade Button")]
    [SerializeField] Button upgradeButton;
    [SerializeField] Text   priceText;
    public Color buttonActiveColor = new Color(0.50f, 0.10f, 0.80f);
    public Color buttonMaxColor    = new Color(0.35f, 0.35f, 0.35f);

    TowerUpgradeComponent boundUpgrade;
    UpgradeStat           boundStat;

    void Awake()
    {
        if (statIcon != null)
        {
            statIcon.color = statIconColor;
            if (statIconSprite != null) statIcon.sprite = statIconSprite;
        }

        if (upgradeButton != null)
        {
            var colors = upgradeButton.colors;
            colors.normalColor      = buttonActiveColor;
            colors.highlightedColor = buttonActiveColor * 1.2f;
            colors.disabledColor    = buttonMaxColor;
            upgradeButton.colors    = colors;

            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }
    }

    public void Bind(TowerUpgradeComponent upgrade, UpgradeStat stat)
    {
        boundUpgrade = upgrade;
        boundStat    = stat;
        Refresh();
    }

    public void Unbind()
    {
        boundUpgrade = null;
        if (upgradeButton != null) upgradeButton.interactable = false;
        if (priceText != null) priceText.text = string.Empty;
    }

    void OnUpgradeClicked()
    {
        if (boundUpgrade == null) return;
        GameplayFacade.I?.BuyUpgrade(boundUpgrade, boundStat);
    }

    void Refresh()
    {
        if (boundUpgrade == null) return;

        int  current = boundUpgrade.GetCurrentLevel(boundStat);
        int  max     = boundUpgrade.GetMaxLevel(boundStat);
        bool isMaxed = current >= max;

        for (int i = 0; i < levelBlocks.Length; i++)
        {
            if (levelBlocks[i] == null) continue;
            levelBlocks[i].color = i < current ? filledColor : emptyColor;
            if (levelBlockSprite != null) levelBlocks[i].sprite = levelBlockSprite;
        }

        if (upgradeButton != null)
            upgradeButton.interactable = !isMaxed;

        if (priceText != null)
            priceText.text = isMaxed ? "MAX" : "$" + boundUpgrade.GetNextCost(boundStat);
    }
}
