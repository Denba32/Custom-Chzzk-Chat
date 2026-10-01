using System;
using System.Collections.Generic;
using System.IO;
using Denba.Chzzk.Scripts.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Denba.Chzzk.Data
{
    public enum CharacterState
    {
        Spawn,     // 첫 채팅(등장)
        Idle1,
        Idle2,
        Idle3,
        Move,
        Donation,
        Subscribe,
        Drag,
        Land,      // 영역 밖에서 떨어진 뒤 착지
        LevelUp
    }

    [Serializable]
    public class AnimationStateData
    {
        public List<string> imagePaths = new();
        public float fps = 8f;
    }

    [Serializable]
    public class AnimationData
    {
        public Dictionary<string, AnimationStateData> states = new();

        public AnimationStateData GetState(CharacterState state)
        {
            string key = state.ToString();
            if (!states.TryGetValue(key, out var data))
                states[key] = data = new AnimationStateData();
            return data;
        }

        public static AnimationData CreateDefault()
        {
            var data = new AnimationData();
            data.FillMissingStates();
            return data;
        }

        public void FillMissingStates()
        {
            foreach (CharacterState state in Enum.GetValues(typeof(CharacterState)))
                GetState(state);
        }
    }

    /// <summary>이미지/말풍선/닉네임 배치 값. 모든 애니메이션이 공유한다.</summary>
    [Serializable]
    public class DisplayData
    {
        public float imageScale = 1f;
        public float bubbleScale = 1f;
        public float bubbleOffsetY = 0f;
        public float nicknameOffsetY = 0f;
    }

    [Serializable]
    public class CharacterData
    {
        public string characterName = "NewCharacter";
        public DisplayData display = new();
        public AnimationData animation = AnimationData.CreateDefault();

        private static string GetPath(string name) =>
            Path.Combine(
                Application.persistentDataPath,
                "config", "characters",
                $"{SanitizeFileName(name)}.json");

        public static CharacterData Load(string characterName)
        {
            string path = GetPath(characterName);
            if (!File.Exists(path)) return CreateDefault(characterName);

            try
            {
                var data = JsonConvert.DeserializeObject<CharacterData>(File.ReadAllText(path));

                data.display ??= new DisplayData();
                data.animation ??= AnimationData.CreateDefault();
                data.animation.FillMissingStates();

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterData] 로드 실패({characterName}): {e.Message}");
                return CreateDefault(characterName);
            }
        }

        public void Save()
        {
            try
            {
                string path = GetPath(characterName);
                Util.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterData] 저장 실패: {e.Message}");
            }
        }

        public void Delete()
        {
            string path = GetPath(characterName);
            if (File.Exists(path)) File.Delete(path);
        }

        public static CharacterData CreateDefault(string name) => new()
        {
            characterName = name,
            display = new DisplayData(),
            animation = AnimationData.CreateDefault()
        };

        private static string SanitizeFileName(string name) =>
            string.Concat(name.Split(Path.GetInvalidFileNameChars()));
    }

    [Serializable]
    public class CharacterListData
    {
        public List<string> characterNames = new();

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "config", "characters.json");

        public static CharacterListData Load()
        {
            if (!File.Exists(SavePath)) return new CharacterListData();

            try
            {
                return JsonConvert.DeserializeObject<CharacterListData>(File.ReadAllText(SavePath))
                       ?? new CharacterListData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterListData] 로드 실패: {e.Message}");
                return new CharacterListData();
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
                Debug.LogError($"[CharacterListData] 저장 실패: {e.Message}");
            }
        }
    }
}
