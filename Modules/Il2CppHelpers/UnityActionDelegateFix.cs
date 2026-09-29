#if IL2CPP
using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EHR;

public static class UnityActionDelegateFix
{
    public static void AddListener(this UnityEvent e, Action listener)
    {
        e.AddListener(listener);
    }
}
#endif