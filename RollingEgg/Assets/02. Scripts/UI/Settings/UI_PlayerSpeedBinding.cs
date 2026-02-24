using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class UI_PlayerSpeedBinding : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TMP_InputField _sliderText;

        private ISettingsService _settingsService;

        private void OnEnable()
        {
            _settingsService = ServiceLocator.Get<ISettingsService>();

            _slider.onValueChanged.RemoveListener(OnValueChangedSlider);
            _slider.onValueChanged.AddListener(OnValueChangedSlider);

            _sliderText.onValueChanged.RemoveListener(OnValueChangedText);
            _sliderText.onValueChanged.AddListener(OnValueChangedText);

            var playerSpeed = _settingsService.PlayerSpeed;
            _slider.value = playerSpeed;
            _sliderText.text = playerSpeed.ToString("F0");
        }

        private void OnValueChangedText(string text)
        {
            if (float.TryParse(text, out float value))
            {
                value = Mathf.Clamp(value, _slider.minValue, _slider.maxValue);

                _slider.onValueChanged.RemoveListener(OnValueChangedSlider);
                _slider.value = value;
                _slider.onValueChanged.AddListener(OnValueChangedSlider);

                _sliderText.text = value.ToString("F0");
                _settingsService.PlayerSpeed = value;
            }
        }

        private void OnValueChangedSlider(float value)
        {
            _sliderText.onValueChanged.RemoveListener(OnValueChangedText);
            _sliderText.text = value.ToString("F0");
            _sliderText.onValueChanged.AddListener(OnValueChangedText);

            _settingsService.PlayerSpeed = value;
        }
    }
}
