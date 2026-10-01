using Denba.Chzzk.Data;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.Utils;
using System;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUICharacterDisplay : MonoBehaviour, IOptionPage
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("캐릭터 선택")]
        [SerializeField] private ScrollRect characterTabScrollView;
        [SerializeField] private OptionUICharacterButton characterButtonPrefab;
        [SerializeField] private ViewerCharacter characterPrefab;

        [Space, Header("프리뷰")]
        [SerializeField] private Canvas previewCanvas;
        [SerializeField] private RectTransform previewParent;

        [Space, Header("표시 설정")]
        [SerializeField] private Slider imageScaleSlider;
        [SerializeField] private TextMeshProUGUI imageScaleLabel;
        [SerializeField] private Slider bubbleScaleSlider;
        [SerializeField] private TextMeshProUGUI bubbleScaleLabel;
        [SerializeField] private Slider bubbleOffsetYSlider;
        [SerializeField] private TextMeshProUGUI bubbleOffsetYLabel;
        [SerializeField] private Slider nicknameOffsetYSlider;
        [SerializeField] private TextMeshProUGUI nicknameOffsetYLabel;

        private readonly List<OptionUICharacterButton> _characterButtons = new();
        private OptionUICharacterButton _selectedButton;
        private ViewerCharacter _previewInstance;
        private CharacterData _selectedData;

        public void Initialize()
        {
            DataManager.OnCharacterAdded += HandleCharacterAdded;
            DataManager.OnCharacterRemoved += HandleCharacterRemoved;
            DataManager.OnCharacterRenamed += HandleCharacterRenamed;

            foreach (string name in DataManager.CharacterNames)
                CreateButton(name);

            SetupSliders();

            if (_characterButtons.Count > 0)
                SelectCharacter(_characterButtons[0]);
        }

        public void Apply()
        {
            if (_selectedButton == null || _selectedData == null) return;
            ViewerManager.Instance?.ApplyDisplayToType(_selectedButton.CharacterName, _selectedData.display);
        }

        public void Show() => canvasGroup.SetVisible(true);

        public void Hide() => canvasGroup.SetVisible(false);

        private void OnDestroy()
        {
            DataManager.OnCharacterAdded -= HandleCharacterAdded;
            DataManager.OnCharacterRemoved -= HandleCharacterRemoved;
            DataManager.OnCharacterRenamed -= HandleCharacterRenamed;
        }

        private void OnDisable() => DestroyPreview();

        private void HandleCharacterAdded(string name) => CreateButton(name);

        private void HandleCharacterRemoved(string name)
        {
            var button = _characterButtons.Find(b => b.CharacterName == name);
            if (button == null) return;

            if (_selectedButton == button)
            {
                _selectedButton = null;
                _selectedData = null;
                DestroyPreview();
            }

            _characterButtons.Remove(button);
            Destroy(button.gameObject);
        }

        private void HandleCharacterRenamed(string oldName, string newName)
            => _characterButtons.Find(b => b.CharacterName == oldName)?.SetCharacterName(newName);

        private void CreateButton(string characterName)
        {
            var button = Instantiate(characterButtonPrefab, characterTabScrollView.content);
            button.Initialize();
            button.SetCharacterName(characterName);
            button.OnClickAsObservable().Subscribe(_ => SelectCharacter(button)).AddTo(button);
            _characterButtons.Add(button);
        }

        private void SelectCharacter(OptionUICharacterButton button)
        {
            _selectedButton = button;
            if (!DataManager.TryGetCharacter(button.CharacterName, out var data)) return;

            _selectedData = data;
            RefreshSliders(data.display);
            SpawnPreview(data);
        }

        private void SpawnPreview(CharacterData data)
        {
            DestroyPreview();
            if (characterPrefab == null || previewParent == null) return;

            _previewInstance = Instantiate(characterPrefab, previewParent);
            _previewInstance.SetPreviewMode(true);

            Canvas canvas = previewCanvas != null ? previewCanvas : GetComponentInParent<Canvas>();
            _previewInstance.Initialize(canvas, previewParent);
            _previewInstance.SetupAnimation(data.characterName);
            _previewInstance.Nickname = "시청자 닉네임";
            _previewInstance.ShowChat("채팅 미리보기 텍스트입니다!");
            _previewInstance.ApplyDisplay(data.display);
        }

        private void DestroyPreview()
        {
            if (_previewInstance == null) return;

            Destroy(_previewInstance.gameObject);
            _previewInstance = null;
        }

        private void SetupSliders()
        {
            SetupSlider(imageScaleSlider, 0.5f, 3f, v =>
                ApplyDisplay(d => d.imageScale = v, imageScaleLabel, $"크기: {v:F1}x"));
            SetupSlider(bubbleScaleSlider, 0.5f, 3f, v =>
                ApplyDisplay(d => d.bubbleScale = v, bubbleScaleLabel, $"크기: {v:F1}x"));
            SetupSlider(bubbleOffsetYSlider, -300f, 300f, v =>
                ApplyDisplay(d => d.bubbleOffsetY = v, bubbleOffsetYLabel, $"Y: {v:F0}"));
            SetupSlider(nicknameOffsetYSlider, -300f, 300f, v =>
                ApplyDisplay(d => d.nicknameOffsetY = v, nicknameOffsetYLabel, $"Y: {v:F0}"));
        }

        private static void SetupSlider(Slider slider, float min, float max, Action<float> onChanged)
        {
            if (slider == null) return;

            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => onChanged(v));
        }

        private void ApplyDisplay(Action<DisplayData> setter, TextMeshProUGUI label, string text)
        {
            if (_selectedData == null) return;

            setter(_selectedData.display);
            SetLabel(label, text);
            _previewInstance?.ApplyDisplay(_selectedData.display);
        }

        private void RefreshSliders(DisplayData display)
        {
            if (display == null) return;

            SetSlider(imageScaleSlider, display.imageScale, imageScaleLabel, $"크기: {display.imageScale:F1}x");
            SetSlider(bubbleScaleSlider, display.bubbleScale, bubbleScaleLabel, $"크기: {display.bubbleScale:F1}x");
            SetSlider(bubbleOffsetYSlider, display.bubbleOffsetY, bubbleOffsetYLabel, $"Y: {display.bubbleOffsetY:F0}");
            SetSlider(nicknameOffsetYSlider, display.nicknameOffsetY, nicknameOffsetYLabel, $"Y: {display.nicknameOffsetY:F0}");
        }

        private static void SetSlider(Slider slider, float value, TextMeshProUGUI label, string text)
        {
            if (slider != null) slider.value = value;
            SetLabel(label, text);
        }

        private static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
