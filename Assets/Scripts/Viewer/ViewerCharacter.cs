using Cysharp.Threading.Tasks;
using Denba.Chzzk.Data;
using Denba.Chzzk.Manager;
using Denba.Chzzk.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ViewerCharacter : MonoBehaviour, IDragItem
{
    private const float DefaultFps = 8f;
    private const float BubbleFadeTime = 0.5f;

    private static readonly Vector3[] AreaCorners = new Vector3[4];
    private static readonly Vector3[] SelfCorners = new Vector3[4];
    private static readonly CharacterState[] AllStates = (CharacterState[])Enum.GetValues(typeof(CharacterState));

    [Header("닉네임 텍스트 (캐릭터 하단, 항상 표시)")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private RectTransform _rect;

    [Header("말풍선 루트 오브젝트 (캐릭터 상단)")]
    [SerializeField] private GameObject chatBubbleRoot;

    [Header("말풍선 채팅 텍스트")]
    [SerializeField] private TextMeshProUGUI chatContentText;

    [Header("말풍선 표시 시간 (초)")]
    [SerializeField] private float bubbleDuration = 3f;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 80f;
    [SerializeField] private float dirChangeMin = 2f;
    [SerializeField] private float dirChangeMax = 6f;

    [Header("낙하 설정")]
    [SerializeField] private float fallSpeed = 400f;

    [Header("드래그")]
    [SerializeField] private bool isDragable = true;

    [Header("표시 설정용 RectTransform")]
    [SerializeField] private RectTransform _characterImageRect;
    [SerializeField] private RectTransform _chatBubbleRect;
    [SerializeField] private RectTransform _nicknameRect;

    [Header("레벨 표시")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image expFillImage; // Image Type: Filled, Fill Method: Radial360

    private readonly Dictionary<CharacterState, List<Sprite>> _spriteCache = new();
    private readonly Dictionary<CharacterState, float> _fpsCache = new();

    private GifAnimator _gifAnimator;
    private CanvasGroup _bubbleGroup;
    private RectTransform _canvasRect;
    private RectTransform _spawnArea;
    private Canvas _canvas;
    private CharacterData _animData;

    private Coroutine _bubbleCoroutine;
    private Coroutine _behaviorCoroutine;

    private string _nickname;
    private Vector2 _targetPos;
    private CharacterState _currentState = CharacterState.Idle1;
    private int _frameIndex;
    private float _frameTimer;
    private bool _isDragging;
    private bool _isFalling;

    public bool IsDragable => isDragable;
    public RectTransform Rect => _rect;
    public string CharacterTypeName { get; private set; }
    public bool IsPreview { get; private set; }

    public string Nickname
    {
        get => _nickname;
        set
        {
            _nickname = value;
            if (nicknameText != null) nicknameText.text = value;
        }
    }

    private RectTransform Area => _spawnArea != null ? _spawnArea : _canvasRect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (chatBubbleRoot != null) chatBubbleRoot.SetActive(false);
    }

    private void Start()
    {
        if (!IsPreview)
            _behaviorCoroutine = StartCoroutine(SpawnThenBehavior());
    }

    private void Update()
    {
        if (!IsPreview && !_isDragging && !_isFalling)
            UpdateMovement();
        UpdateAnimation();
    }

    private void OnDestroy() => ReleaseSprites();

    public void Initialize(Canvas canvas, RectTransform spawnArea = null)
    {
        _canvas = canvas;
        _canvasRect = canvas.transform as RectTransform;
        _spawnArea = spawnArea;
    }

    public void SetPreviewMode(bool preview) => IsPreview = preview;

    public void SetupAnimation(string characterTypeName)
    {
        CharacterTypeName = characterTypeName;
        ApplyAnimationData(CharacterData.Load(characterTypeName));
    }

    public void ApplyAnimationData(CharacterData data)
    {
        _animData = data;
        ReleaseSprites();

        foreach (var state in AllStates)
            LoadStateSprites(state);

        _frameIndex = 0;
        _frameTimer = 0f;

        ApplyDisplay(data.display);
    }

    public void ApplyDisplay(DisplayData display)
    {
        if (display == null) return;

        if (_characterImageRect != null)
            _characterImageRect.localScale = Vector3.one * display.imageScale;

        if (_chatBubbleRect != null)
        {
            _chatBubbleRect.localScale = Vector3.one * display.bubbleScale;
            _chatBubbleRect.anchoredPosition = new Vector2(
                _chatBubbleRect.anchoredPosition.x, display.bubbleOffsetY);
        }

        if (_nicknameRect != null)
        {
            _nicknameRect.anchoredPosition = new Vector2(
                _nicknameRect.anchoredPosition.x, display.nicknameOffsetY);
        }
    }

    public void ApplyChatSettings(ChatSettings chat)
    {
        if (chat == null) return;

        bubbleDuration = chat.bubbleDuration;
        if (_nicknameRect != null)
            _nicknameRect.gameObject.SetActive(chat.showNickname);
    }

    public void SetLevel(int level)
    {
        if (levelText != null)
            levelText.text = level.ToString();
    }

    public void SetExpFill(float exp, float required)
    {
        if (expFillImage == null) return;
        expFillImage.fillAmount = required > 0f ? Mathf.Clamp01(exp / required) : 0f;
    }

    public void SetLevelInfo(int level, float exp, float required)
    {
        SetLevel(level);
        SetExpFill(exp, required);
    }

    public void SetFont(TMP_FontAsset font)
    {
        if (font == null) return;

        if (nicknameText != null) nicknameText.font = font;
        if (chatContentText != null) chatContentText.font = font;
        if (levelText != null) levelText.font = font;
    }

    public void TriggerEvent(CharacterState eventState)
        => StartCoroutine(PlayEventOnce(eventState));

    public void PlayState(CharacterState state)
    {
        if (_currentState == state) return;

        _currentState = state;
        _frameIndex = 0;
        _frameTimer = 0f;
    }

    public void ShowChat(string content, Dictionary<string, object> emojis = null)
    {
        if (chatBubbleRoot == null || chatContentText == null)
        {
            Debug.LogWarning($"[ViewerCharacter] {_nickname}: 말풍선 참조가 없습니다.");
            return;
        }

        ShowChatAsync(content, emojis).Forget();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isDragable || _isFalling) return;

        _isDragging = true;
        StopBehavior();
        PlayState(CharacterState.Drag);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        Canvas canvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
        ClampToCanvas();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        _isDragging = false;

        if (IsPreview)
        {
            PlayState(CharacterState.Idle1);
            return;
        }

        if (IsAboveSpawnArea())
            StartCoroutine(FallToBottom());
        else
            RestartBehavior();
    }

    public void ForceDragEnd()
    {
        if (!_isDragging) return;

        _isDragging = false;
        RestartBehavior();
    }

    public void ClampToCanvas()
    {
        RectTransform area = IsPreview ? _spawnArea : _canvasRect;
        if (area == null) return;

        area.GetWorldCorners(AreaCorners);
        _rect.GetWorldCorners(SelfCorners);

        Vector3 offset = Vector3.zero;

        if (SelfCorners[0].x < AreaCorners[0].x) offset.x = AreaCorners[0].x - SelfCorners[0].x;
        if (SelfCorners[2].x > AreaCorners[2].x) offset.x = AreaCorners[2].x - SelfCorners[2].x;
        if (SelfCorners[0].y < AreaCorners[0].y) offset.y = AreaCorners[0].y - SelfCorners[0].y;
        if (SelfCorners[2].y > AreaCorners[2].y) offset.y = AreaCorners[2].y - SelfCorners[2].y;

        _rect.position += offset;
    }

    public static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[ViewerCharacter] 파일 없음: {path}");
            return null;
        }

        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.LoadImage(File.ReadAllBytes(path));
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ViewerCharacter] 로드 실패({path}): {e.Message}");
            return null;
        }
    }

    private float GetFps(CharacterState state)
        => _fpsCache.TryGetValue(state, out float fps) ? fps : DefaultFps;

    /// <summary>해당 상태의 1회 재생 시간. 스프라이트가 없으면 0.</summary>
    private float GetClipDuration(CharacterState state)
        => _spriteCache.TryGetValue(state, out var sprites) && sprites.Count > 0
            ? sprites.Count / GetFps(state)
            : 0f;

    private void LoadStateSprites(CharacterState state)
    {
        if (_animData == null) return;

        var stateData = _animData.animation.GetState(state);
        var sprites = new List<Sprite>(stateData.imagePaths.Count);

        foreach (string path in stateData.imagePaths)
        {
            var sprite = LoadSprite(path);
            if (sprite != null) sprites.Add(sprite);
        }

        _spriteCache[state] = sprites;
        _fpsCache[state] = stateData.fps;
    }

    private void ReleaseSprites()
    {
        foreach (var sprites in _spriteCache.Values)
        {
            foreach (var sprite in sprites)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        _spriteCache.Clear();
        _fpsCache.Clear();
    }

    private void UpdateAnimation()
    {
        if (characterImage == null) return;
        if (!_spriteCache.TryGetValue(_currentState, out var sprites) || sprites.Count == 0) return;

        float frameDuration = 1f / GetFps(_currentState);

        _frameTimer += Time.deltaTime;
        if (_frameTimer < frameDuration) return;

        _frameTimer -= frameDuration;
        _frameIndex = (_frameIndex + 1) % sprites.Count;
        characterImage.sprite = sprites[_frameIndex];
    }

    private void UpdateMovement()
    {
        if (_canvasRect == null) return;

        _rect.anchoredPosition = Vector2.MoveTowards(
            _rect.anchoredPosition, _targetPos, moveSpeed * Time.deltaTime);

        if (_characterImageRect != null && Mathf.Abs(_targetPos.x - _rect.anchoredPosition.x) > 1f)
        {
            float dir = _targetPos.x < _rect.anchoredPosition.x ? -1f : 1f;
            float scale = Mathf.Abs(_characterImageRect.localScale.y);
            _characterImageRect.localScale = new Vector3(dir * scale, scale, scale);
        }

        ClampToSpawnArea();
    }

    // 캐릭터가 영역보다 작을 때만 영역 안으로 밀어 넣는다.
    private void ClampToSpawnArea()
    {
        RectTransform area = Area;
        if (area == null) return;

        area.GetWorldCorners(AreaCorners);
        _rect.GetWorldCorners(SelfCorners);

        float areaW = AreaCorners[2].x - AreaCorners[0].x;
        float areaH = AreaCorners[2].y - AreaCorners[0].y;
        float charW = SelfCorners[2].x - SelfCorners[0].x;
        float charH = SelfCorners[2].y - SelfCorners[0].y;

        Vector3 offset = Vector3.zero;

        if (charW <= areaW)
        {
            if (SelfCorners[0].x < AreaCorners[0].x) offset.x = AreaCorners[0].x - SelfCorners[0].x;
            if (SelfCorners[2].x > AreaCorners[2].x) offset.x = AreaCorners[2].x - SelfCorners[2].x;
        }
        if (charH <= areaH)
        {
            if (SelfCorners[0].y < AreaCorners[0].y) offset.y = AreaCorners[0].y - SelfCorners[0].y;
            if (SelfCorners[2].y > AreaCorners[2].y) offset.y = AreaCorners[2].y - SelfCorners[2].y;
        }

        if (offset != Vector3.zero) _rect.position += offset;
    }

    private void PickNewTarget()
    {
        RectTransform area = Area;
        if (area == null || _rect.parent is not RectTransform parent) return;

        area.GetWorldCorners(AreaCorners);
        _rect.GetWorldCorners(SelfCorners);
        for (int i = 0; i < 4; i++)
        {
            AreaCorners[i] = parent.InverseTransformPoint(AreaCorners[i]);
            SelfCorners[i] = parent.InverseTransformPoint(SelfCorners[i]);
        }

        float minX = AreaCorners[0].x, minY = AreaCorners[0].y;
        float maxX = AreaCorners[2].x, maxY = AreaCorners[2].y;

        float charW = SelfCorners[2].x - SelfCorners[0].x;
        float charH = SelfCorners[2].y - SelfCorners[0].y;

        float xMin = minX + charW * 0.5f;
        float xMax = maxX - charW * 0.5f;
        float yMax = maxY - charH;

        float x = xMin >= xMax ? (minX + maxX) * 0.5f : UnityEngine.Random.Range(xMin, xMax);
        float y = yMax <= minY ? minY : UnityEngine.Random.Range(minY, yMax);

        _targetPos = new Vector2(x, y);
    }

    private float GetSpawnAreaBottomY()
    {
        RectTransform area = Area;
        if (area == null || _rect.parent is not RectTransform parent) return 0f;

        area.GetWorldCorners(AreaCorners);
        return parent.InverseTransformPoint(AreaCorners[0]).y;
    }

    private bool IsAboveSpawnArea()
    {
        RectTransform area = Area;
        if (area == null || _rect.parent is not RectTransform parent) return false;

        area.GetWorldCorners(AreaCorners);
        return _rect.anchoredPosition.y > parent.InverseTransformPoint(AreaCorners[2]).y;
    }

    private void StopBehavior()
    {
        if (_behaviorCoroutine == null) return;

        StopCoroutine(_behaviorCoroutine);
        _behaviorCoroutine = null;
    }

    private void RestartBehavior()
    {
        StopBehavior();
        _behaviorCoroutine = StartCoroutine(BehaviorLoop());
    }

    private IEnumerator SpawnThenBehavior()
    {
        float spawnDuration = GetClipDuration(CharacterState.Spawn);
        if (spawnDuration > 0f)
        {
            PlayState(CharacterState.Spawn);
            yield return new WaitForSeconds(spawnDuration);
        }

        _behaviorCoroutine = StartCoroutine(BehaviorLoop());
    }

    private IEnumerator BehaviorLoop()
    {
        while (true)
        {
            if (_isDragging || _isFalling) yield break;

            PickNewTarget();
            PlayState(CharacterState.Move);

            while (Vector2.Distance(_rect.anchoredPosition, _targetPos) > 1f)
            {
                if (_isDragging || _isFalling) yield break;
                yield return null;
            }

            PlayState(CharacterState.Idle1);

            float wait = UnityEngine.Random.Range(dirChangeMin, dirChangeMax);
            for (float elapsed = 0f; elapsed < wait; elapsed += Time.deltaTime)
            {
                if (_isDragging || _isFalling) yield break;
                yield return null;
            }
        }
    }

    private IEnumerator FallToBottom()
    {
        _isFalling = true;

        float bottomY = GetSpawnAreaBottomY();

        while (_rect.anchoredPosition.y > bottomY)
        {
            float y = Mathf.MoveTowards(_rect.anchoredPosition.y, bottomY, fallSpeed * Time.deltaTime);
            _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, y);
            yield return null;
        }

        _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, bottomY);

        PlayState(CharacterState.Land);
        float landDuration = GetClipDuration(CharacterState.Land);
        if (landDuration > 0f)
            yield return new WaitForSeconds(landDuration);

        _isFalling = false;
        if (!_isDragging)
            RestartBehavior();
    }

    private IEnumerator PlayEventOnce(CharacterState eventState)
    {
        float duration = GetClipDuration(eventState);
        if (duration <= 0f) yield break;

        StopBehavior();

        CharacterState prevState = _currentState;
        PlayState(eventState);

        yield return new WaitForSeconds(duration);

        PlayState(prevState);

        if (!_isDragging && !_isFalling && !IsPreview)
            _behaviorCoroutine = StartCoroutine(BehaviorLoop());
    }

    private async UniTaskVoid ShowChatAsync(string content, Dictionary<string, object> emojis)
    {
        _gifAnimator?.StopAnimation();

        string finalText = content;
        TMP_SpriteAsset finalAsset = null;
        List<string> gifIds = null;

        var cache = EmojiCache.Instance;
        if (emojis is { Count: > 0 } && cache != null)
        {
            List<TMP_SpriteAsset> spriteAssets;
            (finalText, spriteAssets, gifIds) = await cache.ProcessChat(content, emojis);

            if (spriteAssets is { Count: > 0 })
            {
                finalAsset = spriteAssets[0];
                finalAsset.fallbackSpriteAssets = spriteAssets.GetRange(1, spriteAssets.Count - 1);
            }
        }

        chatContentText.spriteAsset = finalAsset;
        chatContentText.text = finalText;

        // GIF 코루틴은 오브젝트가 활성 상태여야 돌 수 있으므로 말풍선을 먼저 켠다.
        chatBubbleRoot.SetActive(true);

        if (_gifAnimator == null)
            _gifAnimator = chatBubbleRoot.AddComponent<GifAnimator>();
        _gifAnimator.SetAnimations(gifIds, cache);

        if (IsPreview) return;

        if (_bubbleCoroutine != null) StopCoroutine(_bubbleCoroutine);
        _bubbleCoroutine = StartCoroutine(HideBubbleAfter(bubbleDuration));
    }

    private IEnumerator HideBubbleAfter(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration - BubbleFadeTime));

        if (_bubbleGroup == null && !chatBubbleRoot.TryGetComponent(out _bubbleGroup))
            _bubbleGroup = chatBubbleRoot.AddComponent<CanvasGroup>();

        for (float elapsed = 0f; elapsed < BubbleFadeTime; elapsed += Time.deltaTime)
        {
            _bubbleGroup.alpha = 1f - elapsed / BubbleFadeTime;
            yield return null;
        }

        _gifAnimator?.StopAnimation();
        chatBubbleRoot.SetActive(false);
        _bubbleGroup.alpha = 1f;
        _bubbleCoroutine = null;
    }
}
