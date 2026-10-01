using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using Denba.Chzzk.Manager;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Denba.Chzzk.Networks
{
    public class ChzzkSocketClient : SingletonMono<ChzzkSocketClient>
    {
        private const string ApiBase = "https://openapi.chzzk.naver.com";
        private const string SessionUserUrl = ApiBase + "/open/v1/sessions/auth";
        private const string SubscribeChatUrl = ApiBase + "/open/v1/sessions/events/subscribe/chat";
        private const string SubscribeDonationUrl = ApiBase + "/open/v1/sessions/events/subscribe/donation";
        private const string SubscribeSubUrl = ApiBase + "/open/v1/sessions/events/subscribe/subscription";

        private SocketIOUnity _socket;
        private string _sessionKey;
        private string _channelId;

        public bool IsConnected { get; private set; }

        public override UniTask Initialize() => UniTask.CompletedTask;

        public void Connect(string channelId, Action onConnected = null, Action<string> onError = null)
        {
            _channelId = channelId;
            ConnectAsync(onConnected, onError).Forget();
        }

        public void Disconnect()
        {
            _socket?.Disconnect();
            IsConnected = false;
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }

        private async UniTaskVoid ConnectAsync(Action onConnected, Action<string> onError)
        {
            bool tokenOk = false;
            string tokenErr = null;
            await AuthManager.Instance.EnsureValidToken(() => tokenOk = true, err => tokenErr = err);

            if (!tokenOk)
            {
                onError?.Invoke("토큰 갱신 실패: " + tokenErr);
                return;
            }

            string sessionUrl = await GetSessionUrlAsync();
            if (sessionUrl == null)
            {
                onError?.Invoke("세션 URL 발급 실패");
                return;
            }

            ConnectSocketIO(sessionUrl, onConnected, onError);
        }

        private async UniTask<string> GetSessionUrlAsync()
        {
            using var req = UnityWebRequest.Get(SessionUserUrl);
            req.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.AccessToken);
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ChzzkSocketClient] 세션 요청 실패: {req.error}");
                return null;
            }

            var raw = JObject.Parse(req.downloadHandler.text);
            string url = raw["content"]?["url"]?.ToString() ?? raw["url"]?.ToString();

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[ChzzkSocketClient] 세션 URL 파싱 실패");
                return null;
            }

            return url;
        }

        // 세션 URL(https://host:443?auth=XXXX)에서 호스트와 auth 값을 분리해 연결한다.
        private void ConnectSocketIO(string sessionUrl, Action onConnected, Action<string> onError)
        {
            var uri = new Uri(sessionUrl);
            string host = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            string auth = System.Web.HttpUtility.ParseQueryString(uri.Query)["auth"] ?? "";

            _socket = new SocketIOUnity(host, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
                Query = new Dictionary<string, string> { { "auth", auth } },
                EIO = EngineIO.V3, // Socket.IO v2 서버
                Reconnection = false,
            });

            _socket.OnConnected += (_, _) => IsConnected = true;

            _socket.OnDisconnected += (_, _) =>
            {
                IsConnected = false;
                Debug.LogWarning("[ChzzkSocketClient] 소켓 연결 끊김");
            };

            _socket.OnError += (_, e) =>
            {
                Debug.LogError($"[ChzzkSocketClient] 소켓 에러: {e}");
                onError?.Invoke(e);
            };

            _socket.On("SYSTEM", response => HandleSystem(response.ToString(), onConnected));

            RegisterEvent<ChatEventData>("CHAT",
                d => ChzzkEventDispatcher.Instance?.OnChatReceived(d));
            RegisterEvent<DonationEventData>("DONATION",
                d => ChzzkEventDispatcher.Instance?.OnDonationReceived(d));
            RegisterEvent<SubscriptionEventData>("SUBSCRIPTION",
                d => ChzzkEventDispatcher.Instance?.OnSubscriptionReceived(d));

            _socket.Connect();
        }

        private void HandleSystem(string raw, Action onConnected)
        {
            var data = TryParseFirst<JObject>(raw);
            if (data == null) return;

            string sessionKey = data["data"]?["sessionKey"]?.ToString();
            if (data["type"]?.ToString() != "connected" || string.IsNullOrEmpty(sessionKey)) return;

            _sessionKey = sessionKey;
            RunOnMainThread(() => SubscribeAllAsync(onConnected).Forget());
        }

        private void RegisterEvent<T>(string eventName, Action<T> handler) where T : class
        {
            _socket.On(eventName, response =>
            {
                var data = TryParseFirst<T>(response.ToString());
                if (data != null)
                    RunOnMainThread(() => handler(data));
            });
        }

        private static void RunOnMainThread(Action action)
        {
            async UniTaskVoid Run()
            {
                await UniTask.SwitchToMainThread();
                action();
            }
            Run().Forget();
        }

        // 서버가 배열로 감싸거나 JSON 문자열로 한 번 더 감싸서 보내는 경우를 모두 처리한다.
        private static T TryParseFirst<T>(string raw) where T : class
        {
            try
            {
                var token = JToken.Parse(raw);

                if (token is JArray arr && arr.Count > 0)
                    token = arr[0];

                if (token.Type == JTokenType.String)
                    token = JToken.Parse(token.ToString());

                return token.ToObject<T>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TryParseFirst<{typeof(T).Name}>] 실패: {e.Message}");
                return null;
            }
        }

        private async UniTaskVoid SubscribeAllAsync(Action onDone)
        {
            await SubscribeEventAsync(SubscribeChatUrl);
            await SubscribeEventAsync(SubscribeDonationUrl);
            await SubscribeEventAsync(SubscribeSubUrl);
            onDone?.Invoke();
        }

        private async UniTask SubscribeEventAsync(string url)
        {
            string urlWithParam = $"{url}?sessionKey={UnityWebRequest.EscapeURL(_sessionKey)}";

            using var req = new UnityWebRequest(urlWithParam, UnityWebRequest.kHttpVerbPOST);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.AccessToken);
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[ChzzkSocketClient] 구독 실패 ({url}): {req.error}");
        }

        [Serializable]
        public class ChatEventData
        {
            public string channelId;
            public string senderChannelId;
            public ChatProfile profile;
            public string content;
            public long messageTime;
            public Dictionary<string, object> emojis;
        }

        [Serializable]
        public class ChatProfile
        {
            public string nickname;
            public string userRoleCode;
            public bool verifiedMark;
        }

        [Serializable]
        public class DonationEventData
        {
            public string donationType;
            public string channelId;
            public string donatorChannelId;
            public string donatorNickname;
            public string payAmount;
            public string donationText;
        }

        [Serializable]
        public class SubscriptionEventData
        {
            public string channelId;
            public string subscriberChannelId;
            public string subscriberNickname;
            public int tierNo;
            public string tierName;
            public int month;
        }
    }
}
