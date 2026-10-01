using Cysharp.Threading.Tasks;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.Utils;
using Kirurobo;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUI : UIBase
    {
        [Header("메뉴 탭")]
        [SerializeField] private Toggle generalTab;
        [SerializeField] private Toggle characterTab;
        [SerializeField] private Toggle animationTab;
        [SerializeField] private Button exitButton;

        [Header("페이지")]
        [SerializeField] private OptionUIGeneral generalPage;
        [SerializeField] private OptionUICharacterDisplay characterPage;
        [SerializeField] private OptionUICharacterAnimation animationPage;

        [Header("하단 버튼")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;

        [SerializeField] private Button creditButton;

        private UniWindowController _controller;

        public override UniTask OnOpen(params object[] args)
        {
            _controller = FindFirstObjectByType<UniWindowController>();

            generalPage?.Initialize();
            characterPage?.Initialize();
            animationPage?.Initialize();

            ShowPage(0);
            SubscribeEvents();
            return UniTask.CompletedTask;
        }

        public override UniTask OnClose(params object[] args) => UniTask.CompletedTask;

        private void SubscribeEvents()
        {
            BindTab(generalTab, 0);
            BindTab(characterTab, 1);
            BindTab(animationTab, 2);

            exitButton?.OnClickAsObservableFirst()
                .Subscribe(_ => Managers.UI.Open<ExitUI>(UILayer.Popup).Forget())
                .AddTo(_disposable);

            applyButton?.OnClickAsObservableFirst()
                .Subscribe(_ => OnApply())
                .AddTo(_disposable);

            closeButton?.OnClickAsObservableFirst()
                .Subscribe(Close)
                .AddTo(_disposable);
        }

        private void BindTab(Toggle tab, int pageIndex)
        {
            tab?.OnValueChangedAsObservable()
                .Where(isOn => isOn)
                .Subscribe(_ => ShowPage(pageIndex))
                .AddTo(_disposable);
        }

        private void ShowPage(int index)
        {
            SetPage(generalPage, index == 0);
            SetPage(characterPage, index == 1);
            SetPage(animationPage, index == 2);
        }

        private static void SetPage<T>(T page, bool visible) where T : MonoBehaviour, IOptionPage
        {
            if (page == null) return;

            if (visible) page.Show();
            else page.Hide();
        }

        private void OnApply()
        {
            characterPage?.Apply();
            animationPage?.Apply();

            ViewerManager.Instance?.ApplyChatSettingsToAll(DataManager.App.chat);

            if (_controller != null)
                _controller.isTransparent = DataManager.App.chat.transparent;

            DataManager.SaveAll();
        }
    }
}
