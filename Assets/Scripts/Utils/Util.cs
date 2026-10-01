using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Denba.Chzzk.Scripts.Utils
{
    public static class Util
    {
        public static void CreateDirectory(string path)
        {
            if (Directory.Exists(path)) return;

            Directory.CreateDirectory(path);
        }

        public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
        {
            Transform transform = FindChild<Transform>(go, name, recursive);
            if (transform == null)
                return null;

            return transform.gameObject;
        }
        public static T FindChild<T>(GameObject obj, string name = null, bool recursive = false) where T : UnityEngine.Object
        {
            if (obj == null) return null;

            if (!recursive)
            {
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    Transform transform = obj.transform.GetChild(i);

                    if (string.IsNullOrEmpty(name) || transform.name == name)
                    {
                        T component = transform.GetComponent<T>();
                        if (component != null)
                            return component;
                    }
                }
            }
            else
            {
                foreach (T component in obj.GetComponentsInChildren<T>())
                {
                    if (string.IsNullOrEmpty(name) || component.name == name)
                        return component;
                }
            }
            return null;

        }

        public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }

        public static List<Resolution> GetResolutions()
        {
            var seen = new HashSet<(int, int)>();
            var result = new List<Resolution>();

            foreach (var res in Screen.resolutions)
            {
                if (res.width % 16 != 0 || res.height % 9 != 0)
                    continue;

                if (seen.Add((res.width, res.height)))
                    result.Add(res);
            }

            return result;
        }
    }
}