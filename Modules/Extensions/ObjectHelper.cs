using Il2CppSystem;
using UnityEngine;

namespace EHR
{
    public static class ObjectHelper
    {
        public static void DestroyTranslator(this GameObject obj)
        {
            if (obj == null) return;

            obj.ForEachChild((Action<GameObject>)DestroyTranslator); // False error
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
}