using Cysharp.Threading.Tasks;
using Denba.Chzzk.Manager;
using UniRx;
using UnityEngine;

namespace Denba.Chzzk.Scripts.UI
{
    public abstract class UIBase : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] protected CompositeDisposable _disposable = new();

        private UILayer _layer;

        public virtual UniTask Initialize(int sortingOrder, UILayer layer)
        {
            _canvas.sortingOrder = sortingOrder;
            _layer = layer;
            return UniTask.CompletedTask;
        }

        public abstract UniTask OnOpen(params object[] args);
        public abstract UniTask OnClose(params object[] args);

        public int GetOrder() => _canvas.sortingOrder;

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        public void Close() => UIManager.Instance.Close(this).Forget();

        public void Close(Unit _) => Close();

        private void OnDestroy() => _disposable?.Dispose();

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.ignoreParentGroups = visible;
        }
    }
}
