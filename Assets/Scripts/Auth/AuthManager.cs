using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using Denba.Chzzk.Scripts.Utils;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Denba.Chzzk.Manager
{
    public class AuthManager : SingletonMono<AuthManager>
    {
        private const string AuthBaseUrl = "https://chzzk.naver.com/account-interlock";
        private const string TokenUrl = "https://openapi.chzzk.naver.com/auth/v1/token";
        private const string RevokeUrl = "https://openapi.chzzk.naver.com/auth/v1/token/revoke";
        private const string RedirectUri = "http://localhost:8080/callback";
        private const string CallbackHost = "http://localhost:8080/";

        private const string TokenFileName = "token.bin";
        private static readonly string EncryptionSeed = AesCrypto.MachineSeed("token");

        private const int LoginTimeoutSeconds = 120;
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        private DateTime _accessTokenExpiry = DateTime.MinValue;
        private string _clientId;
        private string _clientSecret;

        private static string TokenFilePath =>
            Path.Combine(Application.persistentDataPath, TokenFileName);

        public override UniTask Initialize() => UniTask.CompletedTask;

        public void Setup(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        /// <summary>저장된 토큰으로 갱신을 먼저 시도하고, 실패하면 브라우저 로그인으로 넘어간다.</summary>
        public async UniTask<bool> AutoLoginAsync()
        {
            if (TryLoadToken(out string savedRefresh, out DateTime savedExpiry))
            {
                RefreshToken = savedRefresh;
                _accessTokenExpiry = savedExpiry;

                if (await TryRefreshAsync())
                    return true;

                Debug.LogWarning("[AuthManager] 토큰 갱신 실패, 브라우저 로그인으로 전환");
            }

            var tcs = new UniTaskCompletionSource<string>();
            await StartLoginInternalAsync(() => tcs.TrySetResult(null), err => tcs.TrySetResult(err ?? "unknown"));
            string error = await tcs.Task;

            if (error != null)
                Debug.LogError($"[AuthManager] 브라우저 로그인 실패: {error}");

            return error == null;
        }

        public void StartLogin(Action onSuccess, Action<string> onError)
            => StartLoginInternalAsync(onSuccess, onError).Forget();

        public async UniTask EnsureValidToken(Action onSuccess, Action<string> onError)
        {
            if (DateTime.UtcNow < _accessTokenExpiry - RefreshMargin)
            {
                onSuccess?.Invoke();
                return;
            }

            await RefreshAccessTokenAsync(onSuccess, onError);
        }

        public async UniTask RevokeAsync(Action onDone = null)
        {
            var body = new Dictionary<string, string>
            {
                { "clientId", _clientId },
                { "clientSecret", _clientSecret },
                { "token", AccessToken },
                { "tokenTypeHint", "access_token" }
            };

            using var req = CreateJsonPost(RevokeUrl, body);
            await req.SendWebRequest();

            DeleteSavedToken();
            onDone?.Invoke();
        }

        public void DeleteSavedToken()
        {
            if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath);
            AccessToken = null;
            RefreshToken = null;
            _accessTokenExpiry = DateTime.MinValue;
        }

        private async UniTask<bool> TryRefreshAsync()
        {
            bool ok = false;
            await RefreshAccessTokenAsync(() => ok = true, _ => { });
            return ok;
        }

        private async UniTask StartLoginInternalAsync(Action onSuccess, Action<string> onError)
        {
            try
            {
                string state = Guid.NewGuid().ToString("N")[..12];
                string loginUrl = $"{AuthBaseUrl}?clientId={_clientId}" +
                                  $"&redirectUri={Uri.EscapeDataString(RedirectUri)}" +
                                  $"&state={state}";

                Application.OpenURL(loginUrl);

                string code = await WaitForCallbackAsync(state);

                var body = new Dictionary<string, string>
                {
                    { "grantType", "authorization_code" },
                    { "clientId", _clientId },
                    { "clientSecret", _clientSecret },
                    { "code", code },
                    { "state", state },
                    { "redirectUri", RedirectUri }
                };
                await PostTokenAsync(body, onSuccess, onError);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthManager] 로그인 실패: {e.Message}");
                onError?.Invoke(e.Message);
            }
        }

        private async UniTask<string> WaitForCallbackAsync(string expectedState)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(CallbackHost);
            listener.Start();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(LoginTimeoutSeconds));

            HttpListenerContext context;
            try
            {
                context = await listener
                    .GetContextAsync()
                    .AsUniTask()
                    .AttachExternalCancellation(cts.Token);
                context.Response.Close();
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"로그인 타임아웃 ({LoginTimeoutSeconds}초)");
            }
            finally
            {
                listener.Stop();
            }

            string code = context.Request.QueryString["code"];
            string state = context.Request.QueryString["state"];

            if (state != expectedState)
                throw new Exception("state 불일치");
            if (string.IsNullOrEmpty(code))
                throw new Exception("인증 코드가 비어있음");

            return code;
        }

        private async UniTask RefreshAccessTokenAsync(Action onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                onError?.Invoke("RefreshToken이 없습니다.");
                return;
            }

            var body = new Dictionary<string, string>
            {
                { "grantType", "refresh_token" },
                { "refreshToken", RefreshToken },
                { "clientId", _clientId },
                { "clientSecret", _clientSecret }
            };

            await PostTokenAsync(body, onSuccess, onError);
        }

        private async UniTask PostTokenAsync(
            Dictionary<string, string> body, Action onSuccess, Action<string> onError)
        {
            using var req = CreateJsonPost(TokenUrl, body);
            await req.SendWebRequest();

            string rawJson = req.downloadHandler.text;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AuthManager] 토큰 요청 실패 ({req.responseCode}): {req.error}");
                onError?.Invoke(req.error);
                return;
            }

            // 응답이 { code, message, content } 로 감싸진 경우와 바로 토큰이 오는 경우 모두 처리
            var token = JsonConvert.DeserializeObject<WrappedTokenResponse>(rawJson)?.content;
            if (token == null || string.IsNullOrEmpty(token.accessToken))
                token = JsonConvert.DeserializeObject<TokenResponse>(rawJson);

            if (token == null || string.IsNullOrEmpty(token.accessToken))
            {
                Debug.LogError("[AuthManager] 토큰 응답 파싱 실패");
                onError?.Invoke("토큰 응답 파싱 실패");
                return;
            }

            ApplyToken(token);
            SaveToken();
            onSuccess?.Invoke();
        }

        private static UnityWebRequest CreateJsonPost(string url, object body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));

            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(payload),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        private void ApplyToken(TokenResponse t)
        {
            AccessToken = t.accessToken;
            RefreshToken = t.refreshToken;

            if (int.TryParse(t.expiresIn, out int expiresIn))
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        }

        private void SaveToken()
        {
            try
            {
                var data = new SavedTokenData
                {
                    refreshToken = RefreshToken,
                    expiry = _accessTokenExpiry.ToString("O")
                };

                string json = JsonConvert.SerializeObject(data);
                File.WriteAllBytes(TokenFilePath, AesCrypto.Encrypt(json, EncryptionSeed));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthManager] 토큰 저장 실패: {e.Message}");
            }
        }

        private bool TryLoadToken(out string refreshToken, out DateTime expiry)
        {
            refreshToken = null;
            expiry = DateTime.MinValue;

            if (!File.Exists(TokenFilePath)) return false;

            try
            {
                string json = AesCrypto.Decrypt(File.ReadAllBytes(TokenFilePath), EncryptionSeed);
                var data = JsonConvert.DeserializeObject<SavedTokenData>(json);

                if (data == null || string.IsNullOrEmpty(data.refreshToken)) return false;

                refreshToken = data.refreshToken;
                expiry = DateTime.Parse(data.expiry, null, DateTimeStyles.RoundtripKind);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthManager] 토큰 로드 실패: {e.Message}");
                return false;
            }
        }

        [Serializable]
        private class SavedTokenData
        {
            public string refreshToken;
            public string expiry;
        }

        [Serializable]
        private class WrappedTokenResponse
        {
            public int code;
            public string message;
            public TokenResponse content;
        }

        [Serializable]
        private class TokenResponse
        {
            public string accessToken;
            public string refreshToken;
            public string tokenType;
            public string expiresIn;
            public string scope;
        }
    }
}
