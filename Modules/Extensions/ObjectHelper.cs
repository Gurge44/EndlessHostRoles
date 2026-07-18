using System;
using System.Linq.Expressions;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace EHR;

public static class ObjectHelper
{
    private static readonly List<Behaviour> BehaviourCache = new(16);

    public static void Destroy(GameObject go)
    {
        if (!go || go.Pointer == IntPtr.Zero) return;

        BehaviourCache.Clear();
        go.GetComponents(BehaviourCache);

        for (int i = 0; i < BehaviourCache.Count; i++)
        {
            try
            {
                Behaviour behaviour = BehaviourCache[i];

                if (behaviour && behaviour.Pointer != IntPtr.Zero)
                    Object.Destroy(behaviour);
            }
            catch { }
        }

        BehaviourCache.Clear();

        Object.Destroy(go);
    }
    
    public static void DestroyTranslator(this GameObject obj)
    {
        if (!obj) return;

        obj.ForEachChild((Il2CppSystem.Action<GameObject>)(x => x.DestroyTranslator()));
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

        while (current)
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
        return OperatingSystem.IsAndroid() ? obj.Cast<T>() : obj.Pointer.CastFast<T>();
    }

    private static T CastFast<T>(this IntPtr ptr) where T : Il2CppObjectBase
    {
        return CastHelper<T>.Cast(ptr);
    }

    private static class CastHelper<T> where T : Il2CppObjectBase
    {
        public static readonly Func<IntPtr, T> Cast;

        static CastHelper()
        {
            ConstructorInfo constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
            ParameterExpression ptr = Expression.Parameter(typeof(IntPtr));
            NewExpression create = Expression.New(constructor!, ptr);
            Expression<Func<IntPtr, T>> lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            Cast = lambda.Compile();
        }
    }

    public static bool TryCastFast<T>(this Il2CppObjectBase obj, out T casted) where T : Il2CppObjectBase
    {
        switch (obj)
        {
            case T t:
                casted = t;
                return true;
            case null:
                casted = null;
                return false;
            default:
                casted = OperatingSystem.IsAndroid()
                    ? obj.Cast<T>()
                    : obj.Pointer.CastFast<T>();

                return casted != null;
        }
    }
}