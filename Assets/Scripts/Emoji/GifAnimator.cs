using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Denba.Chzzk.Utility
{
    /// <summary>
    /// 말풍선 안의 GIF 이모티콘을 재생한다.
    /// TMP_SpriteAsset의 spriteSheet를 프레임마다 교체해 sprite 태그가 움직이는 것처럼 보이게 한다.
    /// </summary>
    public class GifAnimator : MonoBehaviour
    {
        private struct AnimEntry
        {
            public TMP_SpriteAsset spriteAsset;
            public List<GifDecoder.GifFrame> frames;
            public int frameIndex;
            public float timer;
        }

        private readonly List<AnimEntry> _entries = new();
        private Coroutine _animCoroutine;

        /// <summary>재생 목록을 통째로 교체한다. 프레임이 1장뿐인 GIF는 건너뛴다.</summary>
        public void SetAnimations(List<string> gifIds, EmojiCache cache)
        {
            _entries.Clear();

            if (gifIds != null)
            {
                foreach (string id in gifIds)
                {
                    var frames = cache.GetGifFrames(id);
                    var asset = cache.GetSpriteAsset(id);
                    if (frames == null || frames.Count <= 1 || asset == null) continue;

                    _entries.Add(new AnimEntry { spriteAsset = asset, frames = frames });
                }
            }

            if (_entries.Count > 0)
            {
                StopAnimation();
                _animCoroutine = StartCoroutine(AnimLoop());
            }
            else
            {
                StopAnimation();
            }
        }

        public void StopAnimation()
        {
            if (_animCoroutine == null) return;

            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }

        private void OnDisable() => StopAnimation();

        private IEnumerator AnimLoop()
        {
            while (_entries.Count > 0)
            {
                float dt = Time.deltaTime;

                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    e.timer += dt;

                    float delay = e.frames[e.frameIndex].delay;
                    if (e.timer >= delay)
                    {
                        e.timer -= delay;
                        e.frameIndex = (e.frameIndex + 1) % e.frames.Count;
                        ApplyFrame(e.spriteAsset, e.frames[e.frameIndex]);
                    }

                    _entries[i] = e;
                }

                yield return null;
            }
        }

        private static void ApplyFrame(TMP_SpriteAsset asset, GifDecoder.GifFrame frame)
        {
            asset.spriteSheet = frame.texture;
            if (asset.material != null)
                asset.material.mainTexture = frame.texture;

            if (asset.spriteGlyphTable.Count > 0)
            {
                var glyph = asset.spriteGlyphTable[0];
                glyph.sprite = frame.sprite;
                asset.spriteGlyphTable[0] = glyph;
            }

            asset.UpdateLookupTables();
        }
    }
}
