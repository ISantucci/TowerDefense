using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Assets/Scripts/Patterns/Command/ICommand.cs
public interface ICommand
{
    void Execute();
    void Undo();
}

