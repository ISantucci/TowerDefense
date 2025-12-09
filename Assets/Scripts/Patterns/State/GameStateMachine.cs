using UnityEngine;

public class GameStateMachine : MonoBehaviour
{
    public IGameState CurrentState { get; private set; }

    public void ChangeState(IGameState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
    }
}
