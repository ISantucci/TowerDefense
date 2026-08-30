using UnityEngine;

/// <summary>Componente raíz del objeto GameFeel: guarda el runtime y limpia la referencia estática al morir.</summary>
public class GameFeelRoot : MonoBehaviour
{
    public LevelController Runtime;

    void Awake()
    {
        // El objeto "GameFeel" de la escena se registra solo; los sistemas de feedback son sus componentes.
        GameFeel.Current = this;
        if (Runtime == null) Runtime = LevelController.Current;
    }

    void Start()
    {
        if (Runtime == null) Runtime = LevelController.Current;
        ProceduralAudio.Warmup();
    }

    void OnDestroy()
    {
        if (GameFeel.Current == this) GameFeel.Current = null;
    }
}
