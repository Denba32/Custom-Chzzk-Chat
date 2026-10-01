using System;
using System.IO;
using Denba.Chzzk.Scripts.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Denba.Chzzk.Data
{
    [Serializable]
    public class ChatSettings
    {
        /// <summary>TMP FontAsset을 만들 .ttf/.otf 절대 경로</summary>
        public string fontAssetPath = "";

        /// <summary>말풍선 표시 시간(초)</summary>
        public float bubbleDuration = 3f;

        public bool showNickname = true;

        /// <summary>창 배경 투명화</summary>
        public bool transparent = false;
    }

    [Serializable]
    public class AppData
    {
        public ChatSettings chat = new();

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "config", "app.json");

        public static AppData Load()
        {
            string path = SavePath;
            if (!File.Exists(path)) return new AppData();

            try
            {
                var data = JsonConvert.DeserializeObject<AppData>(File.ReadAllText(path));
                if (data == null) return new AppData();

                data.chat ??= new ChatSettings();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppData] 로드 실패: {e.Message}");
                return new AppData();
            }
        }

        public void Save()
        {
            try
            {
                Util.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppData] 저장 실패: {e.Message}");
            }
        }
    }
}
