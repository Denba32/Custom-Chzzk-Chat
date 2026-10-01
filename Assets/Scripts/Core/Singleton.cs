using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Denba.Chzzk.Common
{
    /// <summary>
    /// MonoBehaviour 기반 싱글턴.
    /// Scene에 오브젝트로 배치하는 Manager들은 이것을 상속.
    /// (AuthManager, ConfigManager, ChzzkSocketClient 등)
    /// </summary>
    public abstract class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
    {
        protected CompositeDisposable _disposable = new CompositeDisposable();
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            _disposable?.Dispose();
        }

        public abstract UniTask Initialize();
    }

    /// <summary>
    /// 순수 C# 싱글턴 (MonoBehaviour 불필요한 유틸 클래스용).
    /// </summary>
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        public abstract UniTask Initialize(CancellationToken token = default);
    }
}