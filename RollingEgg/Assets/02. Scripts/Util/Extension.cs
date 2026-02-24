using UnityEngine;

namespace RollingEgg
{
    public static class Extension
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();

            return component;
        }

        public static bool IsValild(this GameObject go)
        {
            return go != null && go.activeSelf;
        }
    }
}
