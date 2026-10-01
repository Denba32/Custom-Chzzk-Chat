using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ResourcePathConfigSO", menuName = "ScriptableObject/Resources/ResourcePathConfigSO", order = 0)]
public class ResourcePathConfigSO : ScriptableObject
{
    public List<Object> resourceTargets;
    public List<string> resourceAddress;

#if UNITY_EDITOR
    private const string ResourcesFolder = "/Resources/";

    // Assets/.../Resources/Xxx/Yyy.prefab → Xxx/Yyy
    private void OnValidate()
    {
        if (resourceTargets == null || resourceTargets.Count == 0) return;

        resourceAddress = new List<string>();

        foreach (var target in resourceTargets)
        {
            if (target == null) continue;

            string fullPath = AssetDatabase.GetAssetPath(target);
            int index = fullPath.IndexOf(ResourcesFolder, System.StringComparison.Ordinal);

            if (index < 0)
            {
                Debug.LogWarning($"[ResourcePathConfigSO] Resources 폴더 외부 에셋: {fullPath}");
                continue;
            }

            string relative = fullPath.Substring(index + ResourcesFolder.Length);
            resourceAddress.Add(System.IO.Path.ChangeExtension(relative, null));
        }

        EditorUtility.SetDirty(this);
    }
#endif
}
