using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using UnityEngine;

namespace RollingEgg
{
    public class UI_Title : UI_Scene
    {
        private bool _isSceneChanging;

        private IAudioService _audioService;

        public async override UniTask InitializeAsync()
        {
            _audioService = ServiceLocator.Get<IAudioService>();

            await UniTask.Yield();
        }

        public override void OnShow()
        {
            if (_audioService != null)
            {
                _audioService.PlayBGM(EBGMKey.BGM_Nurture);
            }
        }

        public void OnClickLobby()
        {
            if (_isSceneChanging)
                return;

            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            ChangeSceneWithFade(ESceneUIType.Lobby).Forget();
        }

        public void OnClickSetting()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        //public void OnClickNurture()
        //{
        //    UIManager.Instance.ShowScene(ESceneUIType.Nurture);
        //}

        public void OnClickCredits()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Credits);
        }

        public void OnClickExit()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        private async UniTaskVoid ChangeSceneWithFade(ESceneUIType sceneType)
        {
            _isSceneChanging = true;
            try
            {
                await UIManager.Instance.ShowSceneWithFadeAsync(sceneType);
            }
            finally
            {
                _isSceneChanging = false;
            }
        }
    }
}
