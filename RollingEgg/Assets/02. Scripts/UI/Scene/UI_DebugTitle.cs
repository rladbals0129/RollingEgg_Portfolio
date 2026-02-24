using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using UnityEngine;

namespace RollingEgg
{
    public class UI_DebugTitle : UI_Scene
    {
        private IRunningService _runningService;

        public async override UniTask InitializeAsync()
        {
            _runningService = ServiceLocator.Get<IRunningService>();

            await UniTask.Yield();
        }

        public void OnClickStart()
        {
            // TODO MapService에서 맵 선택
            _runningService.SetRunningEgg(1, "blue");
            _runningService.OnStartRunning(1, 1);
        }

        public void OnClickSetting()
        {
            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        public void OnClickExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
