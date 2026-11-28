#if IL2CPP
using System;
using UnityEngine.Events;

namespace EHR;

public static class UnityActionDelegateFix
{
    public static void AddListener(this UnityEvent e, Action listener)
    {
        e.AddListener(listener);
    }
}
#endif