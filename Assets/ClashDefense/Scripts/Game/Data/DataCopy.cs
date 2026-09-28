using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Copia profunda de los datos del núcleo. Lo que sale de un ScriptableObject hacia una partida es una COPIA:
    /// si la partida o una mejora la modifican, el asset en disco no cambia (Unity, arquitectura con ScriptableObjects:
    /// "copiá los datos a un valor de runtime para no cambiar el que está guardado").
    /// </summary>
    public static class DataCopy
    {
        public static T Clone<T>(T src) where T : class
        {
            if (src == null) return null;
            return JsonUtility.FromJson<T>(JsonUtility.ToJson(src));
        }
    }
}
