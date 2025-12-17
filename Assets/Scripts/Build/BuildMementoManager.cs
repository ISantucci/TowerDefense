using UnityEngine;

public class BuildMementoManager : MonoBehaviour
{
    [SerializeField] int maxCheckpoints = 5;
    [SerializeField] TowerFactoryTD towerFactory;

    QueueTF<BuildSnapshot> snapshots = new QueueTF<BuildSnapshot>();

    public bool HasHistory => !snapshots.IsEmpty();

    void Awake()
    {
        if (towerFactory == null)
            towerFactory = FindObjectOfType<TowerFactoryTD>();
    }

    public void SaveCurrent()
    {
        SaveCheckpoint();
    }

    public void RestoreLast()
    {
        LoadLastCheckpoint();
    }

    public void SaveCheckpoint()
    {
        var snap = BuildSnapshot.Capture();
        if (snap == null) return;

        snapshots.Enqueue(snap);

        while (snapshots.Count > maxCheckpoints)
            snapshots.Dequeue();
    }

    void LoadLastCheckpoint()
    {
        if (snapshots.IsEmpty()) return;

        if (towerFactory == null)
            towerFactory = FindObjectOfType<TowerFactoryTD>();

        if (towerFactory == null) return;

        var last = PopLast();
        if (last == null) return;

        last.Restore(towerFactory);
    }

    BuildSnapshot PopLast()
    {
        int n = snapshots.Count;
        if (n <= 0) return null;

        BuildSnapshot last = null;

        for (int i = 0; i < n; i++)
        {
            var x = snapshots.Dequeue();
            if (i == n - 1) last = x;
            else snapshots.Enqueue(x);
        }

        return last;
    }
}
