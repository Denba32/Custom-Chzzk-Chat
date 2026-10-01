using Cysharp.Threading.Tasks;
using Denba.Chzzk.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace Denba.Chzzk.Scripts.UI
{
    public sealed class CreditUI : UIBase
    {
        [SerializeField] private Button closeButton;

        public override UniTask OnOpen(params object[] args)
        {
            closeButton.OnClickAsObservableFirst().Subscribe(Close).AddTo(this);
            return UniTask.CompletedTask;
        }

        public override UniTask OnClose(params object[] args) => UniTask.CompletedTask;
    }
}
