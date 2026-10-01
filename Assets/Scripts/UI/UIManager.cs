using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using Denba.Chzzk.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Denba.Chzzk.Manager
{
    public enum UILayer
    {
        Scene = 1,
        Popup = 50,
        Transition = 100,
    }

    public interface IDragItem : IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        bool IsDragable { get; }
        void ForceDragEnd();
        void ClampToCanvas();
    }

    public class UIManager : SingletonMono<UIManager>
    {
        [SerializeField] private Transform root;

        private readonly Dictionary<UILayer, LinkedList<UIBase>> _layers = new();

        public override UniTask Initialize() => UniTask.CompletedTask;

        /// <summary>프리팹 원본을 복제해 지정한 레이어에 연다.</summary>
        public async UniTask<T> Open<T>(T target, UILayer uiLayer, CancellationToken token = default, params object[] param)
            where T : UIBase
        {
            return await OpenInternal(Instantiate(target), target.name, uiLayer, param);
        }

        /// <summary>Resources/UI 아래 프리팹을 불러와 연다. path가 비면 타입 이름을 사용한다.</summary>
        public async UniTask<T> Open<T>(UILayer uiLayer, string path = "", params object[] param)
            where T : UIBase
        {
            string resourcePath = $"UI/{(string.IsNullOrEmpty(path) ? typeof(T).Name : path)}";

            T ui = ResourceManager.Instance.Instantiate<T>(resourcePath);
            if (ui == null) return null;

            return await OpenInternal(ui, typeof(T).Name, uiLayer, param);
        }

        public async UniTask Close(UIBase ui)
        {
            if (ui == null) return;

            foreach (var pair in _layers)
            {
                var node = pair.Value.Find(ui);
                if (node == null) continue;

                pair.Value.Remove(node);
                if (pair.Value.Count == 0)
                    _layers.Remove(pair.Key);

                break;
            }

            if (ui.gameObject != null)
            {
                await ui.OnClose();
                Destroy(ui.gameObject);
            }
        }

        private async UniTask<T> OpenInternal<T>(T ui, string objectName, UILayer uiLayer, object[] param)
            where T : UIBase
        {
            if (!_layers.TryGetValue(uiLayer, out var list))
                _layers[uiLayer] = list = new LinkedList<UIBase>();

            ui.transform.SetParent(root);
            ui.name = objectName;
            ui.gameObject.SetActive(true);

            int order = list.Count == 0 ? (int)uiLayer : list.Last.Value.GetOrder() + 1;

            await ui.Initialize(order, uiLayer);
            await ui.OnOpen(param);
            _layers[uiLayer].AddLast(ui);

            return ui;
        }
    }
}
