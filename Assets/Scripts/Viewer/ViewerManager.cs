using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using Denba.Chzzk.Data;
using Denba.Chzzk.Networks;
using Denba.Chzzk.Scripts.UI;
using Denba.Chzzk.Scripts.Utils;
using Denba.Chzzk.UI;
using DG.Tweening;
using Kirurobo;
using System.Collections.Generic;
using System.Threading;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Manager
{
    public class ViewerManager : SingletonMono<ViewerManager>
    {
        [SerializeField] private RectTransform accessUIRoot;
        [SerializeField] private Button accessButton;

        [Header("View")]
        [SerializeField] private Canvas viewCanvas;
        [SerializeField] private RectTransform rootTransform;

        [Space, Header("Side")]
        [SerializeField] private RectTransform _sideLayout;
        [SerializeField] private CanvasGroup _sideGroup;
        [SerializeField] private Button optionButton;
        [SerializeField] private Vector2 openPosition;
        [SerializeField] private Vector2 closePosition;
        [SerializeField] private float duration;
        [SerializeField] private Ease sideEase;

        [Space, Header("캐릭터 기본 Prefab")]
        [Tooltip("앱 시작 시 저장된 캐릭터 자동 복원에 사용")]
        [SerializeField] private ViewerCharacter defaultCharacterPrefab;

        [Space, Header("캐릭터가 생성될 부모")]
        [SerializeField] private RectTransform spawnParent;

        [Header("최대 캐릭터 수")]
        [SerializeField] private int maxViewers = 50;

        private const float SpawnMarginX = 50f;

        private readonly Dictionary<string, ViewerCharacter> _characterTypes = new();
        private readonly Dictionary<string, ViewerCharacter> _viewers = new();
        private readonly Queue<string> _spawnOrder = new();

        private UniWindowController _windowController;
        private bool _isOpen;
        private bool _isProcessing;

        public int ViewerCount => _viewers.Count;

        private UniWindowController WindowController
        {
            get
            {
                if (_windowController == null)
                    _windowController = FindObjectOfType<UniWindowController>();
                return _windowController;
            }
        }

        public override UniTask Initialize()
        {
            optionButton.OnClickAsObservableFirst()
                .Subscribe(_ => Managers.UI.Open<OptionUI>(UILayer.Popup).Forget())
                .AddTo(_disposable);

            accessButton.OnClickAsObservable()
                .Subscribe(_ => AccessAsync().Forget())
                .AddTo(_disposable);

            InputManager.Instance.OnClickOption
                .Subscribe(OnOpenOptionLayout)
                .AddTo(_disposable);

            InputManager.Instance.OnControl
                .Subscribe(_ => ToggleClickThrough())
                .AddTo(_disposable);

            SetSideGroupActive(false);
            _isProcessing = false;

            LoadSavedCharacterTypes();

            return UniTask.CompletedTask;
        }

        public ViewerCharacter GetViewer(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return null;

            _viewers.TryGetValue(nickname, out var viewer);
            return viewer;
        }

        public ViewerCharacter GetOrSpawn(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return null;

            if (_viewers.TryGetValue(nickname, out var existing) && existing != null)
                return existing;

            return SpawnViewer(nickname);
        }

        public void RegisterCharacterType(string characterName, ViewerCharacter prefab)
        {
            _characterTypes[characterName] = prefab;
        }

        public void UnregisterCharacterType(string characterName)
        {
            _characterTypes.Remove(characterName);

            var toRemove = new List<string>();
            foreach (var kvp in _viewers)
            {
                if (kvp.Value == null || kvp.Value.CharacterTypeName != characterName) continue;

                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }

            foreach (string key in toRemove)
                _viewers.Remove(key);
        }

        public void ApplyAnimationToType(string characterName, CharacterData data)
        {
            foreach (var viewer in _viewers.Values)
            {
                if (viewer != null && viewer.CharacterTypeName == characterName)
                    viewer.ApplyAnimationData(data);
            }
        }

        public void ApplyDisplayToType(string characterName, DisplayData display)
        {
            foreach (var viewer in _viewers.Values)
            {
                if (viewer != null && viewer.CharacterTypeName == characterName)
                    viewer.ApplyDisplay(display);
            }
        }

        public void ApplyChatSettingsToAll(ChatSettings chat)
        {
            foreach (var viewer in _viewers.Values)
                viewer?.ApplyChatSettings(chat);
        }

        private void ToggleClickThrough()
        {
            var uwc = WindowController;
            if (uwc != null)
                uwc.isClickThrough = !uwc.isClickThrough;
        }

        private void SetClickThrough(bool value)
        {
            var uwc = WindowController;
            if (uwc != null)
                uwc.isClickThrough = value;
        }

        /// <summary>저장된 토큰으로 갱신, 없거나 실패하면 브라우저 로그인 후 소켓에 연결한다.</summary>
        private async UniTaskVoid AccessAsync()
        {
            accessButton.interactable = false;

            if (!await AuthManager.Instance.AutoLoginAsync())
            {
                Managers.UI.Open<AlertUI>(UILayer.Popup, param: "로그인에 실패했습니다.").Forget();
                accessButton.interactable = true;
                return;
            }

            await ConnectSocketAsync();
        }

        private async UniTask ConnectSocketAsync()
        {
            var tcs = new UniTaskCompletionSource();

            ChzzkSocketClient.Instance.Connect(
                channelId: string.Empty,
                onConnected: () =>
                {
                    accessUIRoot.gameObject.SetActive(false);
                    SetClickThrough(true);
                    tcs.TrySetResult();
                },
                onError: err =>
                {
                    Debug.LogError($"[ViewerManager] 소켓 에러: {err}");
                    Managers.UI.Open<AlertUI>(UILayer.Popup, param: err).Forget();
                    SetClickThrough(false);
                    accessButton.interactable = true;
                    tcs.TrySetResult();
                }
            );

            await tcs.Task;
        }

        private void OnOpenOptionLayout(Unit _)
        {
            if (_isProcessing) return;

            _isOpen = !_isOpen;
            var token = Managers.Token.GetToken(this, nameof(OnOpenOptionLayout));
            if (_isOpen) OpenSideLayout(token).Forget();
            else CloseSideLayout(token).Forget();
        }

        private async UniTask OpenSideLayout(CancellationToken token = default)
        {
            _isProcessing = true;
            _sideGroup.alpha = 1;
            SetSideGroupActive(true);

            await _sideLayout.DOAnchorPosX(openPosition.x, duration)
                .SetEase(sideEase)
                .SetLink(_sideLayout.gameObject)
                .AsyncWaitForCompletion();

            _isProcessing = false;
        }

        private async UniTask CloseSideLayout(CancellationToken token = default)
        {
            _isProcessing = true;

            await _sideLayout.DOAnchorPosX(closePosition.x, duration)
                .SetEase(sideEase)
                .SetLink(_sideLayout.gameObject)
                .AsyncWaitForCompletion();

            _sideGroup.alpha = 0;
            SetSideGroupActive(false);
            _isProcessing = false;
        }

        private void SetSideGroupActive(bool active)
        {
            _sideGroup.blocksRaycasts = active;
            _sideGroup.ignoreParentGroups = active;
            _sideGroup.interactable = active;
        }

        private void LoadSavedCharacterTypes()
        {
            if (defaultCharacterPrefab == null)
            {
                Debug.LogWarning("[ViewerManager] defaultCharacterPrefab이 설정되지 않았습니다.");
                return;
            }

            foreach (string name in CharacterListData.Load().characterNames)
                _characterTypes[name] = defaultCharacterPrefab;
        }

        private ViewerCharacter SpawnViewer(string nickname)
        {
            if (_characterTypes.Count == 0)
            {
                Debug.LogWarning("[ViewerManager] 등록된 캐릭터 타입이 없습니다.");
                return null;
            }

            if (_spawnOrder.Count >= maxViewers && _spawnOrder.TryDequeue(out string oldest))
            {
                if (_viewers.TryGetValue(oldest, out var old) && old != null)
                    Destroy(old.gameObject);
                _viewers.Remove(oldest);
            }

            string selectedType = PickRandomType();
            var character = Instantiate(_characterTypes[selectedType], spawnParent);

            if (character.Rect != null && spawnParent != null)
            {
                Rect area = spawnParent.rect;
                character.Rect.anchoredPosition = new Vector2(
                    Random.Range(area.xMin + SpawnMarginX, area.xMax - SpawnMarginX),
                    Random.Range(area.yMin, area.yMax));
            }

            character.Initialize(viewCanvas, spawnParent);
            character.SetupAnimation(selectedType);
            character.Nickname = nickname;
            character.ApplyChatSettings(DataManager.App.chat);

            ApplySavedLevel(character, nickname);

            _viewers[nickname] = character;
            _spawnOrder.Enqueue(nickname);

            return character;
        }

        private static void ApplySavedLevel(ViewerCharacter character, string nickname)
        {
            var levels = ViewerLevelManager.Instance;
            if (levels == null)
            {
                character.SetLevelInfo(1, 0f, 0f);
                return;
            }

            var saved = levels.GetData(nickname);
            int level = saved?.level ?? 1;
            float exp = saved?.exp ?? 0f;
            character.SetLevelInfo(level, exp, levels.GetRequiredExp(level));
        }

        private string PickRandomType()
        {
            int target = Random.Range(0, _characterTypes.Count);
            int i = 0;
            foreach (string key in _characterTypes.Keys)
            {
                if (i++ == target) return key;
            }
            return null;
        }
    }
}
