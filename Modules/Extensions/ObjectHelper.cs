using System;
using System.Linq.Expressions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace EHR;

public static class ObjectHelper
{
    public static void DestroyTranslator(this GameObject obj)
    {
        if (obj == null) return;

        obj.ForEachChild((Il2CppSystem.Action<GameObject>)DestroyTranslator); // False error
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

// From: https://github.com/TheOtherRolesAU/TheOtherRoles/blob/a5126c5d1eae15e8a9def2dc27dc1e3f63681f2c/TheOtherRoles/Utilities/FastDestroyableSingleton.cs
public static unsafe class FastDestroyableSingleton<T> where T : MonoBehaviour
{
    private static readonly IntPtr FieldPtr;
    private static readonly Func<IntPtr, T> CreateObject;

    static FastDestroyableSingleton()
    {
        FieldPtr = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<DestroyableSingleton<T>>.NativeClassPtr, nameof(DestroyableSingleton<T>._instance));
        var constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
        var ptr = Expression.Parameter(typeof(IntPtr));
        var create = Expression.New(constructor!, ptr);
        var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
        CreateObject = lambda.Compile();
    }

    public static T Instance
    {
        get
        {
            IntPtr objectPointer;
            IL2CPP.il2cpp_field_static_get_value(FieldPtr, &objectPointer);
            return objectPointer == IntPtr.Zero ? DestroyableSingleton<T>.Instance : CreateObject(objectPointer);
        }
    }
}

// The codes below I stole from TommyXL
public static class Il2CppCastHelper
{
    public static T CastFast<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is T casted) return casted;
        return obj.Pointer.CastFast<T>();
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
            var constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
            var ptr = Expression.Parameter(typeof(IntPtr));
            var create = Expression.New(constructor!, ptr);
            var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            Cast = lambda.Compile();
        }
    }
}