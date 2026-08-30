using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selector de niveles. Los botones viven en la escena (uno por nivel, con LevelSelectButton) y se
/// cablean acá; el orden del array es el orden de la campaña. btnLevel1 se conserva por compatibilidad.
/// </summary>
public class LevelSelectorUI : MonoBehaviour
{
    [SerializeField] GameStateMachine stateMachine;

    [Header("Legacy")]
    public Button btnLevel1;
    public Button btnBack;

    [Header("Campaña (uno por nivel, en orden)")]
    public LevelSelectButton[] levelButtons;
    public LevelDefinition[] levels;

    [Header("Extras")]
    public Button btnFreeMode;
    public Text freeModeLabel;
    public Button btnResetProgress;

    void Start()
    {
        if (btnLevel1 != null)
            btnLevel1.onClick.AddListener(() => GameFlow.StartLevel(LevelCatalog.First));

        if (btnBack != null)
            btnBack.onClick.AddListener(() => GameFlow.GoToMainMenu());

        if (btnFreeMode != null)
            btnFreeMode.onClick.AddListener(OnToggleFreeMode);

        if (btnResetProgress != null)
            btnResetProgress.onClick.AddListener(OnResetProgress);

        Refresh();
    }

    public void Refresh()
    {
        if (levelButtons == null) return;
        for (int i = 0; i < levelButtons.Length; i++)
        {
            var b = levelButtons[i];
            if (b == null) continue;
            LevelDefinition def = (levels != null && i < levels.Length) ? levels[i] : LevelCatalog.ByIndex(i);
            b.Bind(def, i + 1);
        }
        if (freeModeLabel != null)
            freeModeLabel.text = LevelCatalog.FreeMode ? "Modo libre: ACTIVADO" : "Modo libre: apagado";
    }

    void OnToggleFreeMode()
    {
        LevelCatalog.FreeMode = !LevelCatalog.FreeMode;
        Refresh();
    }

    void OnResetProgress()
    {
        LevelCatalog.ResetProgress();
        Refresh();
    }
}
