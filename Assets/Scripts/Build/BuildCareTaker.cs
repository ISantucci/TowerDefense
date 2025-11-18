// Assets/Scripts/Build/BuildCaretaker.cs
using System.Collections.Generic;

public class BuildCaretaker
{
    readonly Stack<BuildSnapshot> history = new();

    public void Save(BuildSnapshot s)
    {
        if (s != null)
            history.Push(s);
    }

    public BuildSnapshot Pop()
    {
        return history.Count > 0 ? history.Pop() : null;
    }

    public bool HasSnapshots => history.Count > 0;
}
