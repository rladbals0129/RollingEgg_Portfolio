using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.Util;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class ColorKeyUI : MonoBehaviour
    {
        [SerializeField] private EColorKeyType _colorKeyType;
        [SerializeField] private Image _fillImage;

        [Header("Cooldown Setting")]
        [SerializeField] private float _remainingCooldown;
        [SerializeField] private bool _isCooldown;

        private TMP_Text _text;

        private CancellationTokenSource _cooldownCts;

        public EColorKeyType ColorKeyType => _colorKeyType;
        public float RemainingCooldown => _remainingCooldown;
        public bool IsCooldown => _isCooldown;


        private ISettingsService _settingService;

        public void Initialize()
        {
            _settingService = ServiceLocator.Get<ISettingsService>();

            _text = GetComponentInChildren<TMP_Text>();
        }

        public void SettingPathColorKey(EColorType colorType)
        {
            // KeyColor 업데이트
            var keyColor = PathColorUtil.GetColorFromPathColor(colorType);
            UpdateKeyColor(keyColor);

            // KeyText 업데이트
            var keyCode = _settingService.GetColorKey(_colorKeyType);
            var keyText = KeyCodeUtil.GetDisplayString(keyCode);
            var textColor = colorType == EColorType.Black ? Color.white : Color.black;
            UpdateKeyText(keyText, textColor);

            // 쿨타임 업데이트
            _cooldownCts?.Cancel();
            _cooldownCts?.Dispose();
            _cooldownCts = null;

            _isCooldown = false;
            _fillImage.fillAmount = 1f;
            _remainingCooldown = 0f;

            // 오브젝트 활성화
            gameObject.SetActive(true);
        }

        public async UniTask StartCooldown(float duration)
        {
            _isCooldown = true;

            // 기존 쿨타임이 있다면 취소
            _cooldownCts?.Cancel();
            _cooldownCts?.Dispose();
            _cooldownCts = new CancellationTokenSource();

            _fillImage.fillAmount = 0f;
            _remainingCooldown = duration;

            float elapsed = 0f;

            try
            {
                while (elapsed < duration && !_cooldownCts.IsCancellationRequested)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / duration;

                    _remainingCooldown = duration - elapsed;

                    // 쿨타임 이미지 업데이트
                    _fillImage.fillAmount = progress;

                    await UniTask.Yield(_cooldownCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("[ColorKeyUI] Cooldown Canceled");
            }
            finally
            {
                _isCooldown = false;
                _fillImage.fillAmount = 1f;
                _remainingCooldown = 0f;
            }
        }

        private void UpdateKeyColor(Color color)
        {
            _fillImage.color = color;
            _fillImage.fillAmount = 1f;
        }

        private void UpdateKeyText(string keyText, Color textColor)
        {
            _text.text = keyText;
            _text.color = textColor;
        }
    }
}
