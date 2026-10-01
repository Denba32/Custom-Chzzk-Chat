using System.Security.Cryptography;
using System.Text;

namespace Denba.Chzzk.Scripts.Utils
{
    public static class AesCrypto
    {
        /// <summary>소스에 키를 두지 않기 위해 기기 고유 값과 용도 이름으로 시드를 만든다. 다른 PC에서는 복호화되지 않는다.</summary>
        public static string MachineSeed(string purpose)
            => $"Chzzk.{purpose}.{UnityEngine.SystemInfo.deviceUniqueIdentifier}";

        public static byte[] Encrypt(string plainText, string seed)
        {
            using var aes = CreateAes(seed);
            using var encryptor = aes.CreateEncryptor();
            byte[] input = Encoding.UTF8.GetBytes(plainText);
            return encryptor.TransformFinalBlock(input, 0, input.Length);
        }

        public static string Decrypt(byte[] cipherBytes, string seed)
        {
            using var aes = CreateAes(seed);
            using var decryptor = aes.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plain);
        }

        // 키(SHA-256)와 IV(MD5)를 시드에서 파생. 기존 저장 파일과의 호환을 위해 방식 유지.
        private static Aes CreateAes(string seed)
        {
            byte[] seedBytes = Encoding.UTF8.GetBytes(seed);

            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;

            using (var sha256 = SHA256.Create())
                aes.Key = sha256.ComputeHash(seedBytes);
            using (var md5 = MD5.Create())
                aes.IV = md5.ComputeHash(seedBytes);

            return aes;
        }
    }
}
