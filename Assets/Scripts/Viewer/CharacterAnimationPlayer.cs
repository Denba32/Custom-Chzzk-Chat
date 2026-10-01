using System.Collections.Generic;
using Denba.Chzzk.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Animation
{
    /// <summary>Image.sprite를 프레임 단위로 교체해 재생하는 단독용 플레이어.</summary>
    public class CharacterAnimationPlayer : MonoBehaviour
    {
        private const float DefaultFps = 8f;

        [SerializeField] private Image targetImage;

        private readonly Dictionary<CharacterState, List<Sprite>> _spriteCache = new();
        private readonly Dictionary<CharacterState, float> _fpsCache = new();

        private CharacterData _data;
        private CharacterState _currentState = CharacterState.Idle1;
        private int _frameIndex;
        private float _frameTimer;

        public string CharacterName => _data?.characterName;
        public CharacterData Data => _data;
        public CharacterState CurrentState => _currentState;

        private void Start()
        {
            if (_data != null) ApplyData(_data);
        }

        private void Update()
        {
            if (!_spriteCache.TryGetValue(_currentState, out var sprites) || sprites.Count == 0) return;

            float fps = _fpsCache.TryGetValue(_currentState, out float f) ? f : DefaultFps;
            float frameDuration = 1f / fps;

            _frameTimer += Time.deltaTime;
            if (_frameTimer < frameDuration) return;

            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % sprites.Count;

            if (targetImage != null)
                targetImage.sprite = sprites[_frameIndex];
        }

        public void Setup(string characterName) => ApplyData(CharacterData.Load(characterName));

        public void ApplyData(CharacterData data)
        {
            _data = data;
            _spriteCache.Clear();
            _fpsCache.Clear();

            foreach (CharacterState state in System.Enum.GetValues(typeof(CharacterState)))
                LoadState(state);

            _frameIndex = 0;
            _frameTimer = 0f;
        }

        public void ReloadState(CharacterState state)
        {
            _spriteCache.Remove(state);
            _fpsCache.Remove(state);
            LoadState(state);
        }

        public void PlayState(CharacterState state)
        {
            if (_currentState == state) return;

            _currentState = state;
            _frameIndex = 0;
            _frameTimer = 0f;
        }

        public static Sprite LoadSprite(string path) => ViewerCharacter.LoadSprite(path);

        private void LoadState(CharacterState state)
        {
            var stateData = _data.animation.GetState(state);
            var sprites = new List<Sprite>(stateData.imagePaths.Count);

            foreach (string path in stateData.imagePaths)
            {
                var sprite = LoadSprite(path);
                if (sprite != null) sprites.Add(sprite);
            }

            _spriteCache[state] = sprites;
            _fpsCache[state] = stateData.fps;
        }
    }
}
