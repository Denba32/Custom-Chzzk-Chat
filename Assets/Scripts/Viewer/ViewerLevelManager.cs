using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Denba.Chzzk.Scripts.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Denba.Chzzk.Data
{
    [Serializable]
    public class ViewerLevelData
    {
        public string nickname = "";
        public int level = 1;
        public float exp = 0f;
        public bool hasFollowed = false;
    }

    [Serializable]
    public class ViewerLevelSaveData
    {
        public Dictionary<string, ViewerLevelData> viewers = new();
    }

    [Serializable]
    public class ExpTableJson
    {
        public float[] levels;
    }
}

namespace Denba.Chzzk.Manager
{
    using Denba.Chzzk.Data;

    public class ViewerLevelManager : MonoBehaviour
    {
        public static ViewerLevelManager Instance { get; private set; }
        public static event Action<string, int, int> OnLevelUp; // nickname, oldLevel, newLevel

        private const int MaxLevel = 100;
        private const float ChatExp = 1f;
        private const float DonationExpPerUnit = 0.05f;
        private const float FollowExp = 100f;

        private const string SaveFileName = "viewer_levels.bin";
        private static readonly string EncryptionKey = AesCrypto.MachineSeed("viewer-levels");
        private const float AutoSaveInterval = 600f;

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "config", SaveFileName);

        private static string ExpTablePath =>
            Path.Combine(Application.streamingAssetsPath, "exp_table.json");

        private float[] _expTable;
        private ViewerLevelSaveData _saveData = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadExpTable();
            Load();
            StartCoroutine(AutoSaveLoop());
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            if (Instance == this) Save();
        }

        public float GetRequiredExp(int level)
        {
            if (level >= MaxLevel) return float.MaxValue;
            if (level < 1) return _expTable[0];

            int idx = level - 1;
            return idx < _expTable.Length ? _expTable[idx] : float.MaxValue;
        }

        public void AddChatExp(string nickname) => AddExp(nickname, ChatExp);

        public void AddDonationExp(string nickname, int amount) => AddExp(nickname, amount * DonationExpPerUnit);

        /// <summary>팔로우 경험치는 닉네임당 최초 1회만 지급한다.</summary>
        public bool AddFollowExp(string nickname)
        {
            var data = GetOrCreate(nickname);
            if (data.hasFollowed) return false;

            data.hasFollowed = true;
            AddExp(nickname, FollowExp);
            return true;
        }

        public ViewerLevelData GetData(string nickname)
        {
            _saveData.viewers.TryGetValue(nickname, out var data);
            return data;
        }

        public (int level, float exp, float required) GetLevelInfo(string nickname)
        {
            var data = GetOrCreate(nickname);
            return (data.level, data.exp, GetRequiredExp(data.level));
        }

        public Dictionary<string, ViewerLevelData> GetAllData() => _saveData.viewers;

        public void RemoveViewer(string nickname)
        {
            if (_saveData.viewers.Remove(nickname))
                Save();
        }

        public void ClearAll()
        {
            _saveData.viewers.Clear();
            Save();
        }

        public void Save()
        {
            try
            {
                Util.CreateDirectory(Path.GetDirectoryName(SavePath));

                string json = JsonConvert.SerializeObject(_saveData);
                File.WriteAllBytes(SavePath, AesCrypto.Encrypt(json, EncryptionKey));
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelManager] 저장 실패: {e.Message}");
            }
        }

        private void Load()
        {
            _saveData = new ViewerLevelSaveData();
            if (!File.Exists(SavePath)) return;

            try
            {
                string json = AesCrypto.Decrypt(File.ReadAllBytes(SavePath), EncryptionKey);
                _saveData = JsonConvert.DeserializeObject<ViewerLevelSaveData>(json) ?? _saveData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelManager] 로드 실패: {e.Message}");
            }
        }

        private void AddExp(string nickname, float amount)
        {
            if (string.IsNullOrEmpty(nickname)) return;

            var data = GetOrCreate(nickname);
            if (data.level >= MaxLevel) return;

            var character = ViewerManager.Instance?.GetViewer(nickname);
            data.exp += amount;

            while (data.level < MaxLevel)
            {
                float required = GetRequiredExp(data.level);
                if (data.exp < required) break;

                data.exp -= required;
                int oldLevel = data.level++;
                OnLevelUp?.Invoke(nickname, oldLevel, data.level);

                character?.TriggerEvent(CharacterState.LevelUp);
                character?.SetLevelInfo(data.level, data.exp, GetRequiredExp(data.level));
            }

            if (data.level >= MaxLevel)
            {
                data.level = MaxLevel;
                data.exp = 0f;
            }

            character?.SetExpFill(data.exp, GetRequiredExp(data.level));
        }

        private ViewerLevelData GetOrCreate(string nickname)
        {
            if (!_saveData.viewers.TryGetValue(nickname, out var data))
            {
                data = new ViewerLevelData { nickname = nickname };
                _saveData.viewers[nickname] = data;
            }
            return data;
        }

        private IEnumerator AutoSaveLoop()
        {
            var wait = new WaitForSeconds(AutoSaveInterval);
            while (true)
            {
                yield return wait;
                Save();
            }
        }

        private void LoadExpTable()
        {
            if (File.Exists(ExpTablePath))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<ExpTableJson>(File.ReadAllText(ExpTablePath));
                    if (data?.levels is { Length: > 0 })
                    {
                        _expTable = data.levels;
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LevelManager] exp_table.json 로드 실패: {e.Message}");
                }
            }

            _expTable = BuildDefaultTable();
        }

        private static float[] BuildDefaultTable()
        {
            var table = new float[MaxLevel];
            for (int i = 0; i < table.Length; i++)
            {
                int lv = i + 1;
                if (lv <= 10) table[i] = 50 + lv * 20f;
                else if (lv <= 20) table[i] = 300 + (lv - 10) * 50f;
                else if (lv <= 29) table[i] = 800 + (lv - 20) * 100f;
                else if (lv <= 50) table[i] = 2000 + (lv - 30) * 500f;
                else if (lv <= 70) table[i] = 12000 + (lv - 50) * 2000f;
                else if (lv <= 90) table[i] = 52000 + (lv - 70) * 8000f;
                else table[i] = 212000 + (lv - 90) * 30000f;
            }
            return table;
        }
    }
}
