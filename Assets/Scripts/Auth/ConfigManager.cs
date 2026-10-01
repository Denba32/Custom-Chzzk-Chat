using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using Denba.Chzzk.Scripts.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Denba.Chzzk.Manager
{
    public class ConfigManager : SingletonMono<ConfigManager>
    {
        private const string ConfigFileName = "config.bin";
        private static readonly string EncryptionSeed = AesCrypto.MachineSeed("config");

        private static string ConfigFilePath =>
            Path.Combine(Application.persistentDataPath, ConfigFileName);

        [Serializable]
        private class AppConfig
        {
            public string clientId;
            public string clientSecret;
        }

        public override UniTask Initialize() => UniTask.CompletedTask;

        public bool TryLoad(out string clientId, out string clientSecret)
        {
            clientId = null;
            clientSecret = null;

            if (!File.Exists(ConfigFilePath))
            {
                Debug.LogWarning("[ConfigManager] config.bin 없음");
                return false;
            }

            try
            {
                string json = AesCrypto.Decrypt(File.ReadAllBytes(ConfigFilePath), EncryptionSeed);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);

                if (config == null || string.IsNullOrEmpty(config.clientId))
                {
                    Debug.LogError("[ConfigManager] 설정 파일 파싱 실패");
                    return false;
                }

                clientId = config.clientId;
                clientSecret = config.clientSecret;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 설정 읽기 실패: {e.Message}");
                return false;
            }
        }

        public bool Save(string clientId, string clientSecret)
        {
            try
            {
                var config = new AppConfig { clientId = clientId, clientSecret = clientSecret };
                string json = JsonConvert.SerializeObject(config);
                File.WriteAllBytes(ConfigFilePath, AesCrypto.Encrypt(json, EncryptionSeed));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 설정 저장 실패: {e.Message}");
                return false;
            }
        }

        public bool Exists() => File.Exists(ConfigFilePath);

        public void Delete()
        {
            if (File.Exists(ConfigFilePath))
                File.Delete(ConfigFilePath);
        }
    }
}
