using System;
using System.Linq.Expressions;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace EHR;

public static class ObjectHelper
{
    public static void DestroyTranslator(this GameObject obj)
    {
        if (obj == null) return;

        obj.ForEachChild((Il2CppSystem.Action<GameObject>)(x => DestroyTranslator(x)));
        TextTranslatorTMP[] translator = obj.GetComponentsInChildren<TextTranslatorTMP>(true);
        translator?.Do(Object.Destroy);
    }

    public static void DestroyTranslator(this MonoBehaviour obj)
    {
        obj?.gameObject.DestroyTranslator();
    }

    // From: Project Lotus - by Discussions
    public static bool HasParentInHierarchy(this GameObject obj, string parentPath)
    {
        string[] pathParts = parentPath.Split('/');
        int pathIndex = pathParts.Length - 1;
        Transform current = obj.transform;

        while (current != null)
        {
            if (current.name == pathParts[pathIndex])
            {
                pathIndex--;
                if (pathIndex < 0) return true;
            }
            else pathIndex = pathParts.Length - 1;

            current = current.parent;
        }

        return false;
    }
}

// The codes below I stole from TommyXL
public static class Il2CppCastHelper
{
    public static T CastFast<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is T casted) return casted;
        return obj.Cast<T>();
    }
}