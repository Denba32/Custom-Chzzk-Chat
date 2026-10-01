using Denba.Chzzk.Manager;
using Denba.Chzzk.Scripts.Utils;
using Kirurobo;
using UnityEngine;

namespace Denba.Chzzk.Initializer
{
    /// <summary>
    /// 앱 진입점. config.bin을 읽어 AuthManager를 설정하고 저장 데이터를 불러온다.
    /// 로그인은 ViewerManager의 접속 버튼에서 처리한다.
    ///
    /// 최초 설정: firstTimeSetup 체크 → initClientId/Secret 입력 → 실행해 config.bin 저장 → 체크 해제
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        private const string ResolutionIndexKey = "ResolutionIndex";
        private const string FullScreenKey = "IsFullScreen";

        [Header("최초 1회 설정")]
        [SerializeField] private bool firstTimeSetup = false;
        [SerializeField] private string initClientId;
        [SerializeField] private string initClientSecret;

        private void Awake() => ApplySavedResolution();

        private void Start()
        {
            if (firstTimeSetup)
            {
                ConfigManager.Instance.Save(initClientId, initClientSecret);
                Debug.Log("[AppBootstrap] config.bin 저장 완료. firstTimeSetup을 해제하세요.");
                return;
            }

            if (!ConfigManager.Instance.TryLoad(out string clientId, out string clientSecret))
            {
                Debug.LogError("[AppBootstrap] config.bin 없음. firstTimeSetup을 켜고 한 번 실행하세요.");
                return;
            }

            AuthManager.Instance.Setup(clientId, clientSecret);
            DataManager.LoadAll();

            var window = FindFirstObjectByType<UniWindowController>();
            if (window != null)
                window.isTransparent = DataManager.App.chat.transparent;
        }

        private static void ApplySavedResolution()
        {
            var resolutions = Util.GetResolutions();
            int savedIndex = PlayerPrefs.GetInt(ResolutionIndexKey, -1);
            bool isFullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;

            Resolution resolution = savedIndex >= 0 && savedIndex < resolutions.Count
                ? resolutions[savedIndex]
                : Screen.currentResolution;

            Screen.SetResolution(resolution.width, resolution.height, isFullScreen);
        }
    }
}
