using Cysharp.Threading.Tasks;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.UI;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.UI
{
    public class AlertUI : UIBase
    {
        [SerializeField] private Button okButton;
        [SerializeField] private TMP_Text alertText;

        public override UniTask Initialize(int sortingOrder, UILayer layer)
        {
            okButton.OnClickAsObservable().Subscribe(Close).AddTo(this);
            return base.Initialize(sortingOrder, layer);
        }

        public override UniTask OnOpen(params object[] args)
        {
            if (args == null) return UniTask.CompletedTask;

            foreach (var arg in args)
            {
                if (arg is string message)
                    alertText.text = message;
            }

            return UniTask.CompletedTask;
        }

        public override UniTask OnClose(params object[] args) => UniTask.CompletedTask;
    }
}
