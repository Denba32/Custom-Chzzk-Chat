using Cysharp.Threading.Tasks;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.Utils;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUIGeneral : MonoBehaviour, IOptionPage
    {
        private const string PrefResolutionIndex = "ResolutionIndex";
        private const string PrefFullScreen = "IsFullScreen";

        [SerializeField] private CanvasGroup canvasGroup;

        [Space, Header("말풍선 표시 시간")]
        [SerializeField] private Slider bubbleDurationSlider;
        [SerializeField] private TextMeshProUGUI bubbleDurationLabel;

        [Space, Header("닉네임 표시")]
        [SerializeField] private Toggle showNicknameToggle;

        [Space, Header("해상도 표시")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle screenModeToggle;

        [Space, Header("투명화")]
        [SerializeField] private Toggle transparentToggle;

        [Space, Header("크레딧")]
        [SerializeField] private Button creditButton;

        private List<Resolution> _resolutions;
        private int _currentResolutionIndex;
        private bool _isFullScreen;

        public void Initialize()
        {
            var chat = DataManager.App.chat;

            if (bubbleDurationSlider != null)
            {
                bubbleDurationSlider.minValue = 1f;
                bubbleDurationSlider.maxValue = 10f;
                bubbleDurationSlider.value = chat.bubbleDuration;
                bubbleDurationSlider.onValueChanged.AddListener(OnBubbleDurationChanged);
                UpdateDurationLabel(chat.bubbleDuration);
            }

            if (showNicknameToggle != null)
            {
                showNicknameToggle.isOn = chat.showNickname;
                showNicknameToggle.OnValueChangedAsObservable()
                    .Subscribe(isOn => DataManager.App.chat.showNickname = isOn)
                    .AddTo(this);
            }

            InitializeResolution();

            if (transparentToggle != null)
            {
                transparentToggle.isOn = chat.transparent;
                transparentToggle.OnValueChangedAsObservable()
                    .Skip(1)
                    .Subscribe(isOn => DataManager.App.chat.transparent = isOn)
                    .AddTo(this);
            }

            creditButton.OnClickAsObservableFirst()
                .Subscribe(_ => Managers.UI.Open<CreditUI>(UILayer.Popup).Forget())
                .AddTo(this);
        }

        public void Show() => canvasGroup.SetVisible(true);

        public void Hide() => canvasGroup.SetVisible(false);

        private void InitializeResolution()
        {
            _resolutions = Util.GetResolutions();
            _isFullScreen = PlayerPrefs.GetInt(PrefFullScreen, Screen.fullScreen ? 1 : 0) == 1;

            var options = new List<string>(_resolutions.Count);
            for (int i = 0; i < _resolutions.Count; i++)
            {
                var resolution = _resolutions[i];
                options.Add($"{resolution.width} x {resolution.height}");

                if (Screen.width == resolution.width && Screen.height == resolution.height)
                    _currentResolutionIndex = i;
            }

            int saved = PlayerPrefs.GetInt(PrefResolutionIndex, -1);
            if (saved >= 0 && saved < _resolutions.Count)
                _currentResolutionIndex = saved;

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = _currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();

            screenModeToggle.isOn = _isFullScreen;

            resolutionDropdown.onValueChanged.AsObservable().Skip(1).Subscribe(SetResolution).AddTo(this);
            screenModeToggle.OnValueChangedAsObservable().Skip(1).Subscribe(SetScreenMode).AddTo(this);
        }

        private void OnBubbleDurationChanged(float value)
        {
            DataManager.App.chat.bubbleDuration = value;
            UpdateDurationLabel(value);
        }

        private void UpdateDurationLabel(float value)
        {
            if (bubbleDurationLabel != null)
                bubbleDurationLabel.text = $"{value:F1}초";
        }

        private void SetResolution(int index)
        {
            _currentResolutionIndex = index;
            ApplyScreen();

            PlayerPrefs.SetInt(PrefResolutionIndex, index);
            PlayerPrefs.Save();
        }

        private void SetScreenMode(bool isFullScreen)
        {
            _isFullScreen = isFullScreen;
            ApplyScreen();

            PlayerPrefs.SetInt(PrefFullScreen, isFullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyScreen()
        {
            var resolution = _resolutions[_currentResolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, _isFullScreen);
        }
    }
}
