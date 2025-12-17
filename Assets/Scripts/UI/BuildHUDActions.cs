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

    void Start()
    {
        // Start es más seguro que Awake para agarrar singletons
        if (!facade)
            facade = GameplayFacade.I;
    }

    void Update()
    {
        // Si por orden de ejecución quedó null, lo intentamos recuperar
        if (!facade)
            facade = GameplayFacade.I;

        if (!facade) return;

        if (btnUndo != null) btnUndo.interactable = facade.CanUndoBuild;
        if (btnRedo != null) btnRedo.interactable = facade.CanRedoBuild;
        if (btnLoad != null) btnLoad.interactable = facade.CanLoadSnapshot;
        if (btnSave != null) btnSave.interactable = true;
    }

    public void OnUndo()
    {
        if (!facade) facade = GameplayFacade.I;
        facade?.UndoBuild();
    }

    public void OnRedo()
    {
        if (!facade) facade = GameplayFacade.I;
        facade?.RedoBuild();
    }

    public void OnSaveSnapshot()
    {
        if (!facade) facade = GameplayFacade.I;
        facade?.SaveLayout();
    }

    public void OnLoadSnapshot()
    {
        if (!facade) facade = GameplayFacade.I;
        facade?.LoadLayout();
    }
}
