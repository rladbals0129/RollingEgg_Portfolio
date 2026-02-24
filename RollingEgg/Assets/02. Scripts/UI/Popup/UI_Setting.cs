using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using UnityEngine;

namespace RollingEgg
{
    public class UI_Setting : UI_Popup
    {
        private ISettingsService _settingsService;
        private IAudioService _audioService;

        public async override UniTask InitializeAsync()
        {
            _settingsService = ServiceLocator.Get<ISettingsService>();
            _audioService = ServiceLocator.Get<IAudioService>();

            await UniTask.Yield();
        }

        public void OnClickClose()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
        }

        public void OnClickComplete()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            _settingsService.SaveAsync();
          
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

             uiManager.ClosePopup(this);
        }
    }
}
