using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Denba.Chzzk.Utility
{
    /// <summary>RawImage 위에서 GIF 프레임을 재생한다.</summary>
    [RequireComponent(typeof(RawImage))]
    public class GifImage : MonoBehaviour
    {
        private RawImage _rawImage;
        private List<GifDecoder.GifFrame> _frames;
        private Coroutine _playCoroutine;

        private void Awake() => _rawImage = GetComponent<RawImage>();

        private void OnDisable() => Stop();

        public void Play(List<GifDecoder.GifFrame> frames)
        {
            Stop();
            _frames = frames;

            if (frames == null || frames.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            if (frames.Count == 1)
            {
                _rawImage.texture = frames[0].texture;
                return;
            }

            _playCoroutine = StartCoroutine(AnimateLoop());
        }

        public void Stop()
        {
            if (_playCoroutine == null) return;

            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }

        private IEnumerator AnimateLoop()
        {
            for (int i = 0; ; i = (i + 1) % _frames.Count)
            {
                _rawImage.texture = _frames[i].texture;
                yield return new WaitForSeconds(_frames[i].delay);
            }
        }
    }
}
