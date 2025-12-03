using UnityEngine;
using UnityEngine.UI;

public class BuildHUDActions : MonoBehaviour
{
    [Header("Facade")]
    public GameplayFacade facade;

    [Header("Botones")]
    public Button btnUndo;
    public Button btnRedo;
    public Button btnSave;
    public Button btnLoad;

    void Awake()
    {
        // Si no lo asignaste en el inspector, lo toma del singleton
        if (!facade)
            facade = GameplayFacade.I;
    }

    void Update()
    {
        if (!facade) return;

        if (btnUndo != null)
            btnUndo.interactable = facade.CanUndoBuild;

        if (btnRedo != null)
            btnRedo.interactable = facade.CanRedoBuild;

        if (btnLoad != null)
            btnLoad.interactable = facade.CanLoadSnapshot;

        if (btnSave != null)
            btnSave.interactable = true; // siempre se puede guardar
    }

    // ===== METODOS QUE VA A VER EL BUTTON =====

    public void OnUndo()
    {
        facade?.UndoBuild();
    }

    public void OnRedo()
    {
        facade?.RedoBuild();
    }

    public void OnSaveSnapshot()
    {
        facade?.SaveLayout();
    }

    public void OnLoadSnapshot()
    {
        facade?.LoadLayout();
    }
}
