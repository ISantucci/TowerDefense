using UnityEngine;
using UnityEngine.UI;

public class LevelSelectorUI : MonoBehaviour
{
    [SerializeField] GameStateMachine stateMachine;

    public Button btnLevel1;
    public Button btnBack;

    void Start()
    {
        if (btnLevel1 != null)
            btnLevel1.onClick.AddListener(() => stateMachine.ChangeState(new Level1State()));

        if (btnBack != null)
            btnBack.onClick.AddListener(() => stateMachine.ChangeState(new MainMenuState()));
    }
}
