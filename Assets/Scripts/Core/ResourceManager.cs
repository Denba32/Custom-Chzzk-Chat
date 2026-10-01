using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using UnityEngine;

namespace Denba.Chzzk.Manager
{
    public enum ResourceDirectory
    {
        None,
        Prefabs,
        Images,
        Sounds,
        UI
    }

    public class ResourceManager : Singleton<ResourceManager>
    {
        private readonly Dictionary<string, Object> _cache = new();

        public override UniTask Initialize(CancellationToken token = default)
        {
            InitAsync(token).Forget();
            return UniTask.CompletedTask;
        }

        public T Load<T>(string path = "", ResourceDirectory directory = ResourceDirectory.Prefabs) where T : Object
        {
            string fullPath = BuildPath<T>(path, directory);

            if (_cache.TryGetValue(fullPath, out var cached) && cached != null)
                return Convert<T>(cached);

            var obj = Resources.Load<T>(fullPath);
            if (obj == null) return null;

            _cache[fullPath] = obj;
            return obj;
        }

        public async UniTask<T> LoadAsync<T>(
            string path = "", ResourceDirectory directory = ResourceDirectory.Prefabs,
            CancellationToken token = default) where T : Object
        {
            if (token.IsCancellationRequested) return null;

            string fullPath = BuildPath<T>(path, directory);

            if (_cache.TryGetValue(fullPath, out var cached))
                return Convert<T>(cached);

            var obj = await Resources.LoadAsync<T>(fullPath).ToUniTask(cancellationToken: token);
            if (obj == null) return null;

            _cache[fullPath] = obj;
            return Convert<T>(obj);
        }

        public async UniTask<T> LoadAllAsync<T>(string path = "", CancellationToken token = default) where T : Object
        {
            if (token.IsCancellationRequested) return null;

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[ResourceManager] LoadAllAsync: path가 비어있음");
                return null;
            }

            var objs = Resources.LoadAll<T>(path);
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning($"[ResourceManager] LoadAll 결과 없음: {path}");
                return null;
            }

            foreach (var obj in objs)
                _cache.TryAdd($"{path}/{obj.name}", obj);

            await UniTask.Yield(token);
            return objs[0];
        }

        /// <summary>캐시된 리소스 중 바로 위 폴더 이름이 folderName인 것만 모아 반환한다.</summary>
        public List<T> GetByParentFolder<T>(string folderName) where T : Object
        {
            var result = new List<T>();

            foreach (var kv in _cache)
            {
                string path = kv.Key.Replace('\\', '/');
                int lastSlash = path.LastIndexOf('/');
                if (lastSlash <= 0) continue;

                string parentPath = path.Substring(0, lastSlash);
                string parentFolder = parentPath.Substring(parentPath.LastIndexOf('/') + 1);
                if (parentFolder != folderName) continue;

                var converted = Convert<T>(kv.Value);
                if (converted != null)
                    result.Add(converted);
            }

            return result;
        }

        public T Instantiate<T>(string path, ResourceDirectory directory = ResourceDirectory.Prefabs) where T : Object
        {
            var original = Load<T>(path, directory);
            if (original == null)
            {
                Debug.LogError($"{path} 경로에 {typeof(T).Name} 오브젝트가 없습니다.");
                return null;
            }

            var instance = InstantiateInternal<T>(original);
            if (instance != null)
                instance.name = original.name;

            return instance;
        }

        public T Instantiate<T>(Object obj) where T : Object
        {
            if (obj == null) return null;

            var instance = InstantiateInternal<T>(obj);
            if (instance != null)
                instance.name = obj.name;

            return instance;
        }

        public void Clear()
        {
            UnloadCached();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        public async UniTask ClearAsync()
        {
            UnloadCached();
            await Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        private async UniTask InitAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var config = Managers.Instance.ResourcePath;
            if (config == null || config.resourceAddress == null || config.resourceAddress.Count == 0)
            {
                Debug.LogWarning("[ResourceManager] ResourcePathConfigSO가 없거나 비어있음");
                return;
            }

            foreach (string address in config.resourceAddress)
            {
                if (string.IsNullOrEmpty(address)) continue;
                await LoadAllAsync<Object>(address, token);
            }
        }

        private void UnloadCached()
        {
            foreach (var obj in _cache.Values)
            {
                if (obj != null)
                    Resources.UnloadAsset(obj);
            }

            _cache.Clear();
        }

        private static string BuildPath<T>(string path, ResourceDirectory directory)
            => $"{directory}/{(string.IsNullOrEmpty(path) ? typeof(T).Name : path)}";

        private static T Convert<T>(Object obj) where T : Object
        {
            if (obj is T value)
                return value;

            if (typeof(T) == typeof(Sprite) && obj is Texture2D texture)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)) as T;
            }

            return obj switch
            {
                Component component => component.GetComponent<T>(),
                GameObject go => go.GetComponent<T>(),
                _ => null
            };
        }

        private static T InstantiateInternal<T>(Object obj) where T : Object
        {
            return obj switch
            {
                T t => Object.Instantiate(t),
                GameObject go => Object.Instantiate(go).GetComponent<T>(),
                Component c => Object.Instantiate(c.gameObject).GetComponent<T>(),
                _ => null
            };
        }
    }
}
