using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Barra inferior con una carta por torre del roster del nivel. Las cartas (Slot_1..Slot_9) y el tooltip
/// existen en el prefab LevelHUD; acá sólo se llenan con el roster del LevelDefinition.
/// Hotkeys 1..9, marco en la seleccionada, cartas atenuadas si no alcanza el oro.
/// </summary>
public class TowerCatalogUI : MonoBehaviour
{
    [Header("Cartas (Slot_1..Slot_9 del prefab)")]
    public TowerCatalogButton[] slots;

    [Header("Tooltip (cableado en el prefab)")]
    public GameObject tooltip;
    public Image tooltipStrip;
    public Text tipName;
    public Text tipFamily;
    public Text tipAttack;
    public Text tipDps;
    public Text tipRange;
    public Text tipSpecial;
    public Text tipCost;

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    static readonly Color Danger = new Color(1f, 0.4f, 0.35f);

    LevelController lc;
    TowerPlacer placer;
    readonly List<TowerCatalogButton> active = new List<TowerCatalogButton>();
    TowerCatalogButton hovered;
    int money;

    public void Bind(LevelController controller)
    {
        lc = controller;
        placer = lc != null ? lc.Placer : null;
        money = GameManager.I != null ? GameManager.I.Money : 0;

        active.Clear();
        var roster = lc != null ? lc.Roster : null;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                TowerData d = (roster != null && i < roster.Count) ? roster[i] : null;
                s.Bind(this, d, i);
                if (d != null) active.Add(s);
            }
        }

        GameEvents.MoneyChanged += OnMoneyChanged;
        if (placer != null)
        {
            placer.OnTowerSelected += OnTowerSelected;
            placer.OnSelectionCleared += OnSelectionCleared;
            if (placer.HasSelection) OnTowerSelected(placer.SelectedTowerId);
        }
        if (tooltip != null) tooltip.SetActive(false);
        RefreshAffordability();
    }

    void OnDestroy()
    {
        GameEvents.MoneyChanged -= OnMoneyChanged;
        if (placer != null)
        {
            placer.OnTowerSelected -= OnTowerSelected;
            placer.OnSelectionCleared -= OnSelectionCleared;
        }
    }

    // ───────────────────────── interacción ─────────────────────────

    void Update()
    {
        if (lc == null || lc.IsPaused || lc.IsFinished) return;
        for (int i = 0; i < active.Count && i < 9; i++)
        {
            KeyCode main = (KeyCode)((int)KeyCode.Alpha1 + i);
            KeyCode pad = (KeyCode)((int)KeyCode.Keypad1 + i);
            if (Input.GetKeyDown(main) || Input.GetKeyDown(pad))
            {
                OnCardClicked(active[i]);
                break;
            }
        }
    }

    public void OnCardClicked(TowerCatalogButton card)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (lc == null || placer == null || card == null || card.Data == null) return;
        if (lc.IsPaused) return;

        if (placer.HasSelection && placer.SelectedTowerId == card.Data.id)
        {
            placer.CancelSelection();
            return;
        }
        placer.SelectTower(card.Data.id);
        ProceduralAudio.Play(Sfx.Select);
    }

    void OnTowerSelected(TowerId id)
    {
        foreach (var c in active) c.SetSelected(c.Data != null && c.Data.id == id);
    }

    void OnSelectionCleared()
    {
        foreach (var c in active) c.SetSelected(false);
    }

    void OnMoneyChanged(int value)
    {
        money = value;
        RefreshAffordability();
        if (hovered != null && tipCost != null && hovered.Data != null)
            tipCost.color = hovered.Data.cost <= money ? Gold : Danger;
    }

    void RefreshAffordability()
    {
        foreach (var c in active)
            if (c != null && c.Data != null) c.SetAffordable(c.Data.cost <= money);
    }

    // ───────────────────────── tooltip ─────────────────────────

    public void ShowTooltip(TowerCatalogButton card)
    {
        if (card == null || card.Data == null || tooltip == null) return;
        var d = card.Data;
        Color fam = DefensePalette.Family(d.source);

        if (tooltipStrip != null) tooltipStrip.color = fam;
        if (tipName != null) tipName.text = d.DisplayName;
        if (tipFamily != null) { tipFamily.text = DefensePalette.FamilyName(d.source); tipFamily.color = fam; }
        if (tipAttack != null) tipAttack.text = "Ataque: " + DefensePalette.AttackName(d.attackType) + "   ·   Objetivos: " + DefensePalette.TargetsName(d.targets);
        if (tipDps != null)
            tipDps.text = d.fireRate > 0f
                ? "DPS: " + d.Dps.ToString("F1") + "   (" + d.damage + " de daño cada " + d.fireRate.ToString("0.##") + " s)"
                : "DPS: —";
        if (tipRange != null)
        {
            string range = "Alcance: " + d.range.ToString("0.#");
            if (d.minRange > 0f) range += "   ·   Alcance mínimo: " + d.minRange.ToString("0.#");
            tipRange.text = range;
        }
        if (tipSpecial != null)
        {
            string special = !string.IsNullOrEmpty(d.special) ? d.special : d.description;
            tipSpecial.text = string.IsNullOrEmpty(special) ? "" : special;
        }
        if (tipCost != null)
        {
            tipCost.text = "Costo: " + d.cost + " oro";
            tipCost.color = d.cost <= money ? Gold : Danger;
        }

        tooltip.SetActive(true);
        // centrado sobre la carta
        var tipRect = tooltip.transform as RectTransform;
        var cardRect = card.transform as RectTransform;
        if (tipRect != null && cardRect != null && tipRect.parent != null)
        {
            Vector3 local = tipRect.parent.InverseTransformPoint(cardRect.position);
            Vector2 p = tipRect.anchoredPosition;
            p.x = local.x;
            tipRect.anchoredPosition = p;
        }
        hovered = card;
    }

    public void HideTooltip(TowerCatalogButton card)
    {
        if (hovered != null && hovered != card) return;
        hovered = null;
        if (tooltip != null) tooltip.SetActive(false);
    }
}
