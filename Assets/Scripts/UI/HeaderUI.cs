using Denba.Chzzk.Manager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Denba.Chzzk.Scripts.UI
{
    public sealed class HeaderUI : MonoBehaviour, IDragItem
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform content;

        public bool IsDragable => true;

        public void ClampToCanvas() { }

        public void ForceDragEnd() { }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            var delta = eventData.delta / canvas.scaleFactor;
            content.anchoredPosition += delta;
        }

        public void OnEndDrag(PointerEventData eventData) { }
    }
}

