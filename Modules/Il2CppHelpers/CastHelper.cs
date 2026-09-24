#if IL2CPP
using System;
using System.Linq.Expressions;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace EHR;

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
#endif