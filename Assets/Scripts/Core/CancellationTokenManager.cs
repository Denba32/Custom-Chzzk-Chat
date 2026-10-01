using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;

namespace Denba.Chzzk.Manager
{
    public class CancellationTokenManager : Singleton<CancellationTokenManager>
    {
        private readonly Dictionary<string, CancellationTokenSource> _sources = new();

        public override UniTask Initialize(CancellationToken token = default) => UniTask.CompletedTask;

        /// <summary>caller+taskName 키의 토큰을 새로 발급한다. 이전에 발급된 토큰은 취소된다.</summary>
        public CancellationToken GetToken(object caller, string taskName = "default")
        {
            string key = BuildKey(caller, taskName);
            Release(key);

            var cts = new CancellationTokenSource();
            _sources[key] = cts;
            return cts.Token;
        }

        public void Cancel(object caller, string taskName = "default")
            => Release(BuildKey(caller, taskName));

        /// <summary>caller 타입으로 발급된 모든 토큰을 취소한다.</summary>
        public void CancelAll(object caller)
        {
            string prefix = $"{caller.GetType().Name}_";

            var keys = new List<string>();
            foreach (string key in _sources.Keys)
            {
                if (key.StartsWith(prefix))
                    keys.Add(key);
            }

            foreach (string key in keys)
                Release(key);
        }

        private void Release(string key)
        {
            if (!_sources.Remove(key, out var cts)) return;

            cts.Cancel();
            cts.Dispose();
        }

        private static string BuildKey(object caller, string taskName)
            => $"{caller.GetType().Name}_{taskName}";
    }
}
