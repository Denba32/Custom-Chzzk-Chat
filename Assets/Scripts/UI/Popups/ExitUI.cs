using Cysharp.Threading.Tasks;
using Denba.Chzzk.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace Denba.Chzzk.Scripts.UI
{
    public class ExitUI : UIBase
    {
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;

        public override UniTask OnOpen(params object[] args)
        {
            okButton.OnClickAsObservableFirst().Subscribe(_ => Application.Quit()).AddTo(this);
            cancelButton.OnClickAsObservableFirst().Subscribe(Close).AddTo(this);
            return UniTask.CompletedTask;
        }

        public override UniTask OnClose(params object[] args) => UniTask.CompletedTask;
    }
}
