using UnityEngine;
using UnityEngine.UI;

public class TowerUpgradeUI : MonoBehaviour
{
    [Header("Panel Root  (dejar en null para usar este mismo GO)")]
    [SerializeField] GameObject panel;

    [Header("Tower Icon  (cuadrado amarillo provisorio)")]
    [SerializeField] Image towerIcon;
    public Color towerIconColor = Color.yellow;
    public Sprite towerIconSprite;   // asignar cuando haya sprites reales

    [Header("Rows")]
    [SerializeField] TowerUpgradeRowUI damageRow;
    [SerializeField] TowerUpgradeRowUI speedRow;
    [SerializeField] TowerUpgradeRowUI rangeRow;

    [Header("Sell")]
    [SerializeField] Button sellButton;

    TowerUpgradeComponent _currentUpgrade;
    TowerRangeVisualizer _currentRangeVisualizer;

    void Awake()
    {
        if (panel == null) panel = gameObject;
        panel.SetActive(false);
        sellButton?.onClick.AddListener(OnSellClicked);
    }

    void OnEnable()
    {
        TowerUpgradeComponent.Destroyed    += OnUpgradeDestroyed;
        TowerUpgradeComponent.LevelChanged += OnUpgradeLevelChanged;
    }

    void OnDisable()
    {
        TowerUpgradeComponent.Destroyed    -= OnUpgradeDestroyed;
        TowerUpgradeComponent.LevelChanged -= OnUpgradeLevelChanged;
    }

    void OnUpgradeDestroyed(TowerUpgradeComponent destroyed)
    {
        if (destroyed == _currentUpgrade) Hide();
    }

    void OnUpgradeLevelChanged(TowerUpgradeComponent changed)
    {
        if (changed != _currentUpgrade) return;
        damageRow?.Bind(_currentUpgrade, UpgradeStat.Damage);
        speedRow?.Bind(_currentUpgrade, UpgradeStat.AttackSpeed);
        rangeRow?.Bind(_currentUpgrade, UpgradeStat.Range);
        _currentRangeVisualizer?.Refresh();
    }

    public void Show(TowerUpgradeComponent upgrade)
    {
        _currentRangeVisualizer?.Hide();
        _currentUpgrade = upgrade;
        _currentRangeVisualizer = upgrade.GetComponent<TowerRangeVisualizer>();
        panel.SetActive(true);
        RefreshIcon();
        damageRow?.Bind(upgrade, UpgradeStat.Damage);
        speedRow?.Bind(upgrade, UpgradeStat.AttackSpeed);
        rangeRow?.Bind(upgrade, UpgradeStat.Range);
        _currentRangeVisualizer?.Show();
    }

    public void Hide()
    {
        _currentRangeVisualizer?.Hide();
        _currentRangeVisualizer = null;
        _currentUpgrade = null;
        damageRow?.Unbind();
        speedRow?.Unbind();
        rangeRow?.Unbind();
        panel.SetActive(false);
    }

    void OnSellClicked()
    {
        if (_currentUpgrade == null) return;
        bool sold = GameplayFacade.I != null && GameplayFacade.I.SellTower(_currentUpgrade);
        if (sold) Hide();
    }

    void RefreshIcon()
    {
        if (towerIcon == null) return;
        towerIcon.color = towerIconColor;
        if (towerIconSprite != null)
            towerIcon.sprite = towerIconSprite;
    }
}
