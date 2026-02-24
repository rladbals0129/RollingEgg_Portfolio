using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using UnityEngine;

namespace RollingEgg
{
    public class UI_RunningSetting : UI_Popup
    {
        [SerializeField] private GameObject _retryMessageBox;

        private IAudioService _audioService;
        private IRunningService _runningService;

        public async override UniTask InitializeAsync()
        {
            _audioService = ServiceLocator.Get<IAudioService>();
            _runningService = ServiceLocator.Get<IRunningService>();

            await UniTask.Yield();
        }

        public override void OnShow()
        {
            _retryMessageBox.SetActive(false);
            _runningService.PauseRunning();
        }

        public override void OnHide()
        {
            _runningService.ResumeRunning();
        }

        public void OnClickRetry()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            _retryMessageBox.SetActive(true);
        }

        public void OnClickExit()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.CloseAllPopups();

            _runningService.Dispose();
            UIManager.Instance.ShowScene(ESceneUIType.Title);
        }

        public void OnClickClose()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
        }

        public void OnClickRetryMessageBoxYes()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.CloseAllPopups();

            _runningService.OnReStartRunning();
        }

        public void OnClickRetryMessageBoxNo()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            _retryMessageBox.SetActive(false);
        }
    }
}
