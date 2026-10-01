using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Denba.Chzzk.Utility
{
    /// <summary>
    /// 치지직 이모티콘 캐시. 채팅 본문의 "{:id:}" 자리표시자를 TMP sprite 태그로 바꾼다.
    /// 정적 이미지와 GIF 모두 이모티콘 1개당 TMP_SpriteAsset 1개를 만들고,
    /// GIF 프레임 교체는 GifAnimator가 담당한다.
    /// </summary>
    public class EmojiCache : SingletonMono<EmojiCache>
    {
        private const string UserAgent = "Mozilla/5.0 (compatible; UnityPlayer)";

        private readonly Dictionary<string, Texture2D> _staticCache = new();
        private readonly Dictionary<string, List<GifDecoder.GifFrame>> _gifCache = new();
        private readonly Dictionary<string, TMP_SpriteAsset> _spriteAssetCache = new();
        private readonly Dictionary<string, UniTask> _downloadingTasks = new();

        public override UniTask Initialize() => UniTask.CompletedTask;

        public bool IsGif(string url) =>
            !string.IsNullOrEmpty(url) && url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

        public List<GifDecoder.GifFrame> GetGifFrames(string emojiId) =>
            _gifCache.TryGetValue(emojiId, out var frames) ? frames : null;

        public TMP_SpriteAsset GetSpriteAsset(string emojiId) =>
            _spriteAssetCache.TryGetValue(emojiId, out var asset) ? asset : null;

        public TMP_SpriteAsset GetOrCreateSpriteAsset(string emojiId, Texture2D tex)
        {
            if (_spriteAssetCache.TryGetValue(emojiId, out var cached)) return cached;

            var asset = CreateSpriteAsset(emojiId, tex);
            _spriteAssetCache[emojiId] = asset;
            return asset;
        }

        /// <summary>
        /// 이모티콘을 내려받고 본문을 치환한다.
        /// gifIds에는 애니메이션이 필요한(프레임 2장 이상) GIF의 id만 담긴다.
        /// </summary>
        public async UniTask<(string text, List<TMP_SpriteAsset> spriteAssets, List<string> gifIds)> ProcessChat(
            string content, Dictionary<string, object> emojis)
        {
            var gifIds = new List<string>();
            var spriteAssets = new List<TMP_SpriteAsset>();

            if (emojis == null || emojis.Count == 0)
                return (content, spriteAssets, gifIds);

            await DownloadMissingAsync(emojis);

            string text = content;
            foreach (var kvp in emojis)
            {
                string id = kvp.Key;
                string placeholder = $"{{:{id}:}}";
                string replacement = $"[{id}]";

                if (IsGif(kvp.Value as string))
                {
                    if (_gifCache.TryGetValue(id, out var frames) && frames.Count > 0)
                    {
                        spriteAssets.Add(GetOrCreateSpriteAsset(id, frames[0].texture));
                        replacement = SpriteTag(id);
                        if (frames.Count > 1) gifIds.Add(id);
                    }
                }
                else if (_staticCache.TryGetValue(id, out var tex))
                {
                    spriteAssets.Add(GetOrCreateSpriteAsset(id, tex));
                    replacement = SpriteTag(id);
                }

                text = text.Replace(placeholder, replacement);
            }

            return (text, spriteAssets, gifIds);
        }

        private static string SpriteTag(string id) => $"<sprite name=\"{id}\">";

        private async UniTask DownloadMissingAsync(Dictionary<string, object> emojis)
        {
            var tasks = new List<UniTask>();
            foreach (var kvp in emojis)
            {
                if (kvp.Value is not string url || string.IsNullOrEmpty(url)) continue;

                var task = DownloadEmojiAsync(kvp.Key, url);
                if (task.Status != UniTaskStatus.Succeeded)
                    tasks.Add(task);
            }

            if (tasks.Count == 0) return;

            try { await UniTask.WhenAll(tasks); }
            catch (Exception e)
            {
                Debug.LogWarning($"[EmojiCache] 일부 이모티콘 다운로드 실패: {e.Message}");
            }
        }

        private UniTask DownloadEmojiAsync(string emojiId, string url)
        {
            bool gif = IsGif(url);
            if (gif ? _gifCache.ContainsKey(emojiId) : _staticCache.ContainsKey(emojiId))
                return UniTask.CompletedTask;

            if (_downloadingTasks.TryGetValue(emojiId, out var existing))
                return existing;

            var task = DownloadAndStoreAsync(emojiId, url, gif).Preserve();
            _downloadingTasks[emojiId] = task;
            return task;
        }

        private async UniTask DownloadAndStoreAsync(string emojiId, string url, bool gif)
        {
            try
            {
                byte[] bytes = await DownloadBytesAsync(emojiId, url, gif ? "image/gif,image/*,*/*" : "image/*,*/*");
                if (bytes == null || bytes.Length == 0) return;

                if (gif) StoreGif(emojiId, bytes);
                else StoreStatic(emojiId, bytes);
            }
            finally
            {
                _downloadingTasks.Remove(emojiId);
            }
        }

        // UnityWebRequestTexture는 GIF를 읽지 못하고 일부 CDN에서 오류를 내므로 바이트로 받아 직접 디코딩한다.
        private static async UniTask<byte[]> DownloadBytesAsync(string emojiId, string url, string accept)
        {
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", UserAgent);
            req.SetRequestHeader("Accept", accept);

            try { await req.SendWebRequest(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[EmojiCache] 요청 예외 ({emojiId}): {e.Message}");
            }

            if (req.result == UnityWebRequest.Result.Success)
                return req.downloadHandler.data;

            Debug.LogWarning($"[EmojiCache] 다운로드 실패 ({emojiId}): HTTP {req.responseCode} {req.error}");
            return null;
        }

        private void StoreStatic(string emojiId, byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            if (tex.LoadImage(bytes))
            {
                _staticCache[emojiId] = tex;
                return;
            }

            Debug.LogWarning($"[EmojiCache] 이미지 디코딩 실패 ({emojiId})");
            Destroy(tex);
        }

        private void StoreGif(string emojiId, byte[] bytes)
        {
            var frames = GifDecoder.Decode(bytes);
            if (frames.Count > 0)
                _gifCache[emojiId] = frames;
            else
                Debug.LogWarning($"[EmojiCache] GIF 디코딩 실패 ({emojiId})");
        }

        // asset에 material과 스프라이트를 직접 채워 TMP가 null 참조로 터지지 않게 한다.
        private static TMP_SpriteAsset CreateSpriteAsset(string emojiId, Texture2D tex)
        {
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = emojiId;
            asset.spriteSheet = tex;

            // TMP 버전에 따라 테이블이 null일 수 있다.
            if (asset.spriteGlyphTable == null)
                SetField(asset, "m_SpriteGlyphTable", new List<TMP_SpriteGlyph>());
            if (asset.spriteCharacterTable == null)
                SetField(asset, "m_SpriteCharacterTable", new List<TMP_SpriteCharacter>());
            if (asset.spriteInfoList == null)
                asset.spriteInfoList = new List<TMP_Sprite>();

            var shader = Shader.Find("TextMeshPro/Sprite") ?? Shader.Find("UI/Default");
            if (shader == null)
            {
                Debug.LogError("[EmojiCache] 스프라이트 셰이더를 찾을 수 없음");
                return asset;
            }

            asset.material = new Material(shader) { name = $"EmojiMat_{emojiId}", mainTexture = tex };

            var sprite = Sprite.Create(
                tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = emojiId;

            var glyph = new TMP_SpriteGlyph
            {
                index = 0,
                sprite = sprite,
                metrics = new UnityEngine.TextCore.GlyphMetrics(tex.width, tex.height, 0, tex.height, tex.width),
                glyphRect = new UnityEngine.TextCore.GlyphRect(0, 0, tex.width, tex.height),
                scale = 1f,
            };
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0, asset, glyph) { name = emojiId, scale = 1f });
            asset.spriteInfoList.Add(new TMP_Sprite
            {
                name = emojiId,
                sprite = sprite,
                x = 0,
                y = 0,
                width = tex.width,
                height = tex.height,
                xOffset = 0,
                yOffset = tex.height,
                xAdvance = tex.width,
                scale = 1f,
                id = 0,
            });

            asset.UpdateLookupTables();
            return asset;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
            else Debug.LogWarning($"[EmojiCache] 필드 없음: {fieldName}");
        }
    }
}
