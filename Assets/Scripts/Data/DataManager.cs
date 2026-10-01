using Denba.Chzzk.Data;
using System;
using System.Collections.Generic;

namespace Denba.Chzzk.Manager
{
    /// <summary>
    /// 앱 설정과 캐릭터 데이터를 한 곳에서 관리한다.
    /// app.json / characters.json / characters/*.json
    /// </summary>
    public static class DataManager
    {
        private const string CharacterNamePrefix = "캐릭터";

        private static CharacterListData _characterList = new();
        private static readonly Dictionary<string, CharacterData> _characterMap = new();

        public static AppData App { get; private set; } = new();

        public static IReadOnlyDictionary<string, CharacterData> CharacterMap => _characterMap;
        public static IReadOnlyList<string> CharacterNames => _characterList.characterNames;

        public static event Action<string> OnCharacterAdded;
        public static event Action<string> OnCharacterRemoved;
        public static event Action<string, string> OnCharacterRenamed; // (oldName, newName)
        public static event Action<string> OnCharacterDataChanged;
        public static event Action OnAppDataChanged;

        public static void LoadAll()
        {
            App = AppData.Load();

            _characterList = CharacterListData.Load();
            _characterMap.Clear();
            foreach (string name in _characterList.characterNames)
                _characterMap[name] = CharacterData.Load(name);
        }

        public static void SaveAll()
        {
            App.Save();
            foreach (var data in _characterMap.Values)
                data.Save();
            _characterList.Save();
        }

        public static void SaveApp()
        {
            App.Save();
            OnAppDataChanged?.Invoke();
        }

        public static string AddCharacter()
        {
            int index = 1;
            string name = CharacterNamePrefix + index;
            while (_characterList.characterNames.Contains(name))
                name = CharacterNamePrefix + ++index;

            _characterList.characterNames.Add(name);
            _characterMap[name] = CharacterData.CreateDefault(name);

            OnCharacterAdded?.Invoke(name);
            return name;
        }

        public static void RemoveCharacter(string characterName)
        {
            if (_characterMap.Remove(characterName, out var data))
                data.Delete();
            _characterList.characterNames.Remove(characterName);

            OnCharacterRemoved?.Invoke(characterName);
        }

        public static bool RenameCharacter(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;

            newName = newName.Trim();
            if (oldName == newName) return false;
            if (_characterList.characterNames.Contains(newName)) return false;

            int listIndex = _characterList.characterNames.IndexOf(oldName);
            if (listIndex >= 0) _characterList.characterNames[listIndex] = newName;

            if (_characterMap.Remove(oldName, out var data))
            {
                data.Delete();
                data.characterName = newName;
                _characterMap[newName] = data;
            }

            OnCharacterRenamed?.Invoke(oldName, newName);
            return true;
        }

        public static CharacterData GetCharacter(string characterName)
        {
            _characterMap.TryGetValue(characterName, out var data);
            return data;
        }

        public static bool TryGetCharacter(string characterName, out CharacterData data)
            => _characterMap.TryGetValue(characterName, out data);

        /// <summary>FPS, 이미지 경로 등을 바꾼 뒤 구독자에게 알린다.</summary>
        public static void NotifyCharacterChanged(string characterName)
            => OnCharacterDataChanged?.Invoke(characterName);

        public static void SaveCharacter(string characterName)
        {
            if (_characterMap.TryGetValue(characterName, out var data))
                data.Save();
        }

        public static void SaveCharacterList() => _characterList.Save();
    }
}
