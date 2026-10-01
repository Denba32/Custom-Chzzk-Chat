using Cysharp.Threading.Tasks;
using Denba.Chzzk.Common;
using UnityEngine;

namespace Denba.Chzzk.Manager
{
    public class Managers : SingletonMono<Managers>
    {
        private const int TargetFrameRate = 60;

        [SerializeField] private ResourcePathConfigSO resourcePathConfigSO;

        public ResourcePathConfigSO ResourcePath => resourcePathConfigSO;

        public static CancellationTokenManager Token => CancellationTokenManager.Instance;
        public static InputManager Input => InputManager.Instance;
        public static ViewerManager View => ViewerManager.Instance;
        public static ResourceManager Resource => ResourceManager.Instance;
        public static UIManager UI => UIManager.Instance;

        private void Start() => Initialize();

        public override UniTask Initialize()
        {
            Application.targetFrameRate = TargetFrameRate;
            InitAsync().Forget();
            return UniTask.CompletedTask;
        }

        private async UniTask InitAsync()
        {
            await Resource.Initialize();
            await UI.Initialize();
            await Token.Initialize();
            await Input.Initialize();
            await View.Initialize();
        }
    }
}
