// Assets/Scripts/UI/BuildHUDActions.cs
using UnityEngine;
using UnityEngine.UI;

public class BuildHUDActions : MonoBehaviour
{
    public BuildInvoker invoker;          // arrastrá tu BuildInvoker_GO
    public BuildMementoManager memento;   // arrastrá tu BuildMementoManager_GO

    [Header("Botones (opcionales)")]
    public Button btnUndo;
    public Button btnRedo;
    public Button btnSave;
    public Button btnLoad;

    void Update()
    {
        if (btnUndo != null) btnUndo.interactable = invoker != null && invoker.CanUndo;
        if (btnRedo != null) btnRedo.interactable = invoker != null && invoker.CanRedo;
        if (btnLoad != null) btnLoad.interactable = memento != null && memento.HasHistory;
        // Save normalmente siempre activo
    }

    public void OnUndo()
    {
        Debug.Log("[HUD] OnUndo()");
        invoker?.Undo();
    }

    public void OnRedo()
    {
        invoker?.Redo();
    }

    public void OnSaveSnapshot()
    {
        Debug.Log("[HUD] OnSaveSnapshot()");
        memento?.SaveCurrent();
    }

    public void OnLoadSnapshot()
    {
        memento?.RestoreLast();

    }
}
