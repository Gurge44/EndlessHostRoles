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

    public static void DestroyTranslator(this MonoBehaviour obj) => obj?.gameObject.DestroyTranslator();
}
