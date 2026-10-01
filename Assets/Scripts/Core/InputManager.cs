using System;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using UniRx;

namespace Denba.Chzzk.Manager
{
    public class InputManager : SingletonMono<InputManager>
    {
        private readonly Subject<Unit> _onClickOption = new();
        private readonly Subject<Unit> _onControl = new();

        private InputSystem_Actions _inputActions;

        public IObservable<Unit> OnClickOption => _onClickOption;
        public IObservable<Unit> OnControl => _onControl;

        public override UniTask Initialize()
        {
            _disposable.Add(_onClickOption);
            _disposable.Add(_onControl);

            _inputActions = new InputSystem_Actions();
            _inputActions.UI.Option.started += _ => _onClickOption.OnNext(Unit.Default);
            _inputActions.UI.Control.started += _ => _onControl.OnNext(Unit.Default);
            _inputActions.Enable();

            return UniTask.CompletedTask;
        }

        protected override void OnDestroy()
        {
            _inputActions?.Dispose();
            base.OnDestroy();
        }
    }
}
