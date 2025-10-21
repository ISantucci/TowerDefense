using UnityEngine;

public class WaypointsPath : MonoBehaviour
{
    public Transform[] points;
    void OnValidate()
    {
        
        int n = transform.childCount;
        points = new Transform[n];
        for (int i = 0; i < n; i++) points[i] = transform.GetChild(i);
    }
}
