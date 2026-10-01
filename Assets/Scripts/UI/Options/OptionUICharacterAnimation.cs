using Cysharp.Threading.Tasks;
using Denba.Chzzk.Data;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.Utils;
using Denba.Chzzk.Utility;
using Kirurobo;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Scripts.UI
{
    public class OptionUICharacterAnimation : MonoBehaviour, IOptionPage
    {
        private const float MinFps = 1f;
        private const float MaxFps = 30f;

        [Header("Character Tab")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private ScrollRect characterTabScrollView;
        [SerializeField] private Button addCharacterButton;
        [SerializeField] private OptionUICharacterButton _characterButtonPrefab;
        [SerializeField] private TMP_InputField characterNameInput;

        [Space, Header("State Tab")]
        [SerializeField] private ScrollRect stateTabScrollView;
        [SerializeField] private StateTabItem stateTabItemPrefab;

        [Space, Header("FPS")]
        [SerializeField] private Slider fpsSlider;
        [SerializeField] private TextMeshProUGUI fpsLabel;

        [Space, Header("Character Image Container")]
        [SerializeField] private ScrollRect characterContainerScrollView;
        [SerializeField] private OptionUICreateCharacterImageButton createCharacterImageButton;
        [SerializeField] private OptionUICharacterLoadedUI characterLoadedUIPrefab;

        [Space, Header("Viewer Character Prefab")]
        [SerializeField] private ViewerCharacter characterPrefab;

        [Space, Header("Preview")]
        [SerializeField] private Image previewImage;

        private readonly List<OptionUICharacterButton> _characterButtons = new();
        private readonly List<OptionUICharacterLoadedUI> _loadedUIList = new();
        private readonly List<Sprite> _previewSprites = new();
        private readonly List<Sprite> _loadedSprites = new();

        private OptionUICharacterButton _selectedCharacterButton;
        private StateTabItem _selectedStateTab;

        private float _previewFps = 8f;
        private int _previewFrame;
        private float _previewTimer;
        private bool _previewPlaying;

        public void Initialize()
        {
            DataManager.OnCharacterAdded += HandleCharacterAdded;
            DataManager.OnCharacterRemoved += HandleCharacterRemoved;
            DataManager.OnCharacterRenamed += HandleCharacterRenamed;

            addCharacterButton.OnClickAsObservable()
                .Subscribe(_ => DataManager.AddCharacter())
                .AddTo(this);

            createCharacterImageButton.OnClickAsObservable
                .Subscribe(_ => OnAddImage())
                .AddTo(this);

            if (fpsSlider != null)
            {
                fpsSlider.minValue = MinFps;
                fpsSlider.maxValue = MaxFps;
                fpsSlider.onValueChanged.AddListener(OnFpsChanged);
            }

            foreach (string name in DataManager.CharacterNames)
            {
                CreateButton(name);
                RegisterCharacterType(name);
            }

            BuildStateTabs();

            if (_characterButtons.Count > 0)
                SelectCharacterButton(_characterButtons[0]);

            SetPreviewActive(false);

            if (characterNameInput != null)
                characterNameInput.onEndEdit.AddListener(_ => StartCoroutine(ApplyNameNextFrame()));
        }

        public void Apply()
        {
            if (_selectedCharacterButton == null) return;

            string name = _selectedCharacterButton.CharacterName;
            if (DataManager.TryGetCharacter(name, out var data))
                ViewerManager.Instance?.ApplyAnimationToType(name, data);
        }

        public void Show() => canvasGroup.SetVisible(true);

        public void Hide() => canvasGroup.SetVisible(false);

        private void OnDestroy()
        {
            DataManager.OnCharacterAdded -= HandleCharacterAdded;
            DataManager.OnCharacterRemoved -= HandleCharacterRemoved;
            DataManager.OnCharacterRenamed -= HandleCharacterRenamed;

            ClearPreviewSprites();
            ClearImageList();
        }

        private void Update()
        {
            if (!_previewPlaying || _previewSprites.Count == 0) return;

            float frameDuration = 1f / _previewFps;
            _previewTimer += Time.deltaTime;
            if (_previewTimer < frameDuration) return;

            _previewTimer -= frameDuration;
            _previewFrame = (_previewFrame + 1) % _previewSprites.Count;

            if (previewImage != null)
                previewImage.sprite = _previewSprites[_previewFrame];
        }

        private void RegisterCharacterType(string name)
        {
            if (characterPrefab != null)
                ViewerManager.Instance?.RegisterCharacterType(name, characterPrefab);
        }

        private void HandleCharacterAdded(string name)
        {
            CreateButton(name);
            RegisterCharacterType(name);

            var button = _characterButtons.Find(b => b.CharacterName == name);
            if (button != null) SelectCharacterButton(button);
        }

        private void HandleCharacterRemoved(string name)
        {
            var button = _characterButtons.Find(b => b.CharacterName == name);
            if (button == null) return;

            if (_selectedCharacterButton == button)
            {
                _selectedCharacterButton = null;
                ClearImageList();
                ClearPreviewSprites();
                SetPreviewActive(false);
            }

            _characterButtons.Remove(button);
            Destroy(button.gameObject);
            ViewerManager.Instance?.UnregisterCharacterType(name);
        }

        private void HandleCharacterRenamed(string oldName, string newName)
        {
            _characterButtons.Find(b => b.CharacterName == oldName)?.SetCharacterName(newName);

            if (_selectedCharacterButton?.CharacterName == newName && characterNameInput != null)
                characterNameInput.text = newName;

            ViewerManager.Instance?.UnregisterCharacterType(oldName);
            RegisterCharacterType(newName);
        }

        private void CreateButton(string characterName)
        {
            var button = Instantiate(_characterButtonPrefab, characterTabScrollView.content);
            button.Initialize();
            button.SetCharacterName(characterName);
            button.OnClickAsObservable().Subscribe(SelectCharacterButton).AddTo(button);
            button.OnDeleteAsObservable().Subscribe(b => DataManager.RemoveCharacter(b.CharacterName)).AddTo(button);

            addCharacterButton.transform.SetAsLastSibling();
            _characterButtons.Add(button);
        }

        private void SelectCharacterButton(OptionUICharacterButton button)
        {
            _selectedCharacterButton = button;

            if (characterNameInput != null)
                characterNameInput.text = button.CharacterName;

            RefreshImageList();
            RefreshPreview();
        }

        // 입력 필드가 확정된 직후에는 텍스트가 아직 갱신 중일 수 있어 한 프레임 뒤에 반영한다.
        private IEnumerator ApplyNameNextFrame()
        {
            yield return null;
            if (_selectedCharacterButton == null) yield break;

            string oldName = _selectedCharacterButton.CharacterName;
            if (!DataManager.RenameCharacter(oldName, characterNameInput.text) && characterNameInput != null)
                characterNameInput.text = oldName;
        }

        private void BuildStateTabs()
        {
            if (stateTabItemPrefab == null) return;

            foreach (CharacterState state in Enum.GetValues(typeof(CharacterState)))
            {
                var item = Instantiate(stateTabItemPrefab, stateTabScrollView.content);
                item.Initialize(state);
                item.OnSelect.Subscribe(SelectState).AddTo(item);
            }
        }

        private void SelectState(StateTabItem item)
        {
            _selectedStateTab = item;

            if (TryGetSelectedData(out var data))
            {
                float fps = data.animation.GetState(item.State).fps;
                if (fpsSlider != null) fpsSlider.value = fps;
                UpdateFpsLabel(fps);
            }

            RefreshImageList();
            RefreshPreview();
        }

        private void OnFpsChanged(float value)
        {
            if (_selectedStateTab == null || !TryGetSelectedData(out var data)) return;

            data.animation.GetState(_selectedStateTab.State).fps = value;
            UpdateFpsLabel(value);
            _previewFps = value;
        }

        private void UpdateFpsLabel(float fps)
        {
            if (fpsLabel != null) fpsLabel.text = $"FPS: {fps:F0}";
        }

        private void OnAddImage()
        {
            if (_selectedCharacterButton == null || _selectedStateTab == null)
            {
                Debug.LogWarning("[OptionUICharacterAnimation] 캐릭터와 상태를 먼저 선택하세요.");
                return;
            }

            OnAddImageAsync().Forget();
        }

        private async UniTaskVoid OnAddImageAsync()
        {
            // 항상 위에 떠 있는 창이 파일 대화상자를 가리지 않도록 잠시 해제한다.
            var window = FindFirstObjectByType<UniWindowController>();
            if (window != null) window.isTopmost = false;

            var selectedPaths = await WindowsFileDialog.OpenMultipleAsync("이미지 파일 선택");

            if (window != null) window.isTopmost = true;

            if (selectedPaths == null || selectedPaths.Count == 0) return;
            if (_selectedStateTab == null || !TryGetSelectedData(out var data)) return;

            var paths = data.animation.GetState(_selectedStateTab.State).imagePaths;
            foreach (string path in selectedPaths)
            {
                if (!paths.Contains(path))
                    paths.Add(path);
            }

            RefreshImageList();
            RefreshPreview();
        }

        private bool TryGetSelectedData(out CharacterData data)
        {
            data = null;
            return _selectedCharacterButton != null
                   && DataManager.TryGetCharacter(_selectedCharacterButton.CharacterName, out data);
        }

        private void RefreshImageList()
        {
            ClearImageList();

            if (_selectedStateTab == null || !TryGetSelectedData(out var data)) return;

            var paths = data.animation.GetState(_selectedStateTab.State).imagePaths;

            foreach (string path in paths)
            {
                var ui = Instantiate(characterLoadedUIPrefab, characterContainerScrollView.content);
                _loadedUIList.Add(ui);

                var sprite = ViewerCharacter.LoadSprite(path);
                if (sprite != null)
                {
                    ui.SetImage(sprite);
                    _loadedSprites.Add(sprite);
                }

                ui.Initialize();
                ui.OnDeleted.Subscribe(item => RemoveImage(paths, item)).AddTo(ui);
                ui.OnPrev.Subscribe(item => MoveImage(paths, item, -1)).AddTo(ui);
                ui.OnNext.Subscribe(item => MoveImage(paths, item, +1)).AddTo(ui);
            }

            createCharacterImageButton.transform.SetAsLastSibling();
        }

        private void RemoveImage(List<string> paths, OptionUICharacterLoadedUI item)
        {
            int index = _loadedUIList.IndexOf(item);
            if (index < 0 || index >= paths.Count) return;

            paths.RemoveAt(index);
            RefreshImageList();
            RefreshPreview();
        }

        private void MoveImage(List<string> paths, OptionUICharacterLoadedUI item, int offset)
        {
            int index = _loadedUIList.IndexOf(item);
            int target = index + offset;
            if (index < 0 || target < 0 || target >= paths.Count) return;

            (paths[index], paths[target]) = (paths[target], paths[index]);
            RefreshImageList();
            RefreshPreview();
        }

        private void ClearImageList()
        {
            foreach (var ui in _loadedUIList)
            {
                if (ui == null) continue;

                Destroy(ui.gameObject);
            }

            foreach (var sprite in _loadedSprites)
                ReleaseSprite(sprite);

            _loadedUIList.Clear();
            _loadedSprites.Clear();
        }

        private void RefreshPreview()
        {
            ClearPreviewSprites();

            if (_selectedStateTab == null || !TryGetSelectedData(out var data))
            {
                SetPreviewActive(false);
                return;
            }

            var stateData = data.animation.GetState(_selectedStateTab.State);
            foreach (string path in stateData.imagePaths)
            {
                var sprite = ViewerCharacter.LoadSprite(path);
                if (sprite != null) _previewSprites.Add(sprite);
            }

            if (_previewSprites.Count == 0)
            {
                SetPreviewActive(false);
                return;
            }

            _previewFps = stateData.fps;
            _previewPlaying = true;
            SetPreviewActive(true);

            if (previewImage != null)
                previewImage.sprite = _previewSprites[0];
        }

        private void ClearPreviewSprites()
        {
            foreach (var sprite in _previewSprites)
                ReleaseSprite(sprite);

            _previewSprites.Clear();
            _previewPlaying = false;
            _previewFrame = 0;
            _previewTimer = 0f;
        }

        private void SetPreviewActive(bool active)
        {
            if (previewImage != null)
                previewImage.gameObject.SetActive(active);
        }

        private static void ReleaseSprite(Sprite sprite)
        {
            if (sprite == null) return;

            Destroy(sprite.texture);
            Destroy(sprite);
        }
    }
}
