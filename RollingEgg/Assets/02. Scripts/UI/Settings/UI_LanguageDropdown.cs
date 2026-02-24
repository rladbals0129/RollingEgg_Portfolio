using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using RollingEgg.Core;

namespace RollingEgg.UI
{
	/// <summary>
	/// 설정 화면의 언어 선택 드롭다운 바인더
	/// - 실행 시 사용 가능한 로캘을 스캔하여 옵션을 구성
	/// - 선택 변경 시 ILocalizationService를 통해 로캘 전환
	/// </summary>
	[RequireComponent(typeof(TMP_Dropdown))]
	public sealed class UI_LanguageDropdown : MonoBehaviour
	{
		private static readonly Dictionary<string, string> LocaleDisplayOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "ko", "한국어" },
			{ "en", "English" },
			{ "ja", "日本語" }, // 일부 환경에서 ja 로만 노출될 수 있으므로 예외 처리
			{ "ja-JP", "日本語" },
			{ "zh", "中文" },
			{ "zh-Hans", "简体中文" },
			{ "zh-CN", "简体中文" },
			{ "zh-SG", "简体中文" },
			{ "zh-Hant", "繁體中文" },
			{ "zh-TW", "繁體中文" },
			{ "zh-HK", "繁體中文" },
			{ "zh-MO", "繁體中文" }
		};

		private TMP_Dropdown _dropdown;
		private ILocalizationService _localizationService;
		private ISettingsService _settingsService;
		private List<Locale> _locales;

		private void Awake()
		{
			_dropdown = GetComponent<TMP_Dropdown>();
		}

		private void OnEnable()
		{
			_localizationService = ServiceLocator.Get<ILocalizationService>();
			_settingsService = ServiceLocator.Get<ISettingsService>();
			BuildOptionsAsync().Forget();
			_dropdown.onValueChanged.AddListener(OnDropdownChanged);
			// 로캘 변경 시 목록 텍스트를 갱신(네이티브 이름 등)
			_localizationService.OnLocaleChanged += OnLocaleChanged;
		}

		private void OnDisable()
		{
			_dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
			if (_localizationService != null)
				_localizationService.OnLocaleChanged -= OnLocaleChanged;
		}

		private void OnLocaleChanged()
		{
			// 표시 이름이 로캘에 따라 달라질 수 있으므로 재구성
			BuildOptionsAsync().Forget();
		}

		private async UniTask BuildOptionsAsync()
		{
			var initOperation = LocalizationSettings.InitializationOperation;
			if (initOperation.IsValid() && !initOperation.IsDone)
				await initOperation.Task;

			_locales = LocalizationSettings.AvailableLocales?.Locales ?? new List<Locale>();
			Debug.Log($"[UI_LanguageDropdown] Available locales: {string.Join(", ", _locales.Select(l => l.Identifier.Code))}");
			_dropdown.options.Clear();
			for (int i = 0; i < _locales.Count; i++)
			{
				var locale = _locales[i];
				string display = GetDisplayName(locale);
				_dropdown.options.Add(new TMP_Dropdown.OptionData(display));
			}

			// 현재/저장된 로캘을 선택 상태로 반영
			var currentCode = GetSavedOrCurrentLocaleCode();
			int index = _locales.FindIndex(l => string.Equals(l.Identifier.Code, currentCode, StringComparison.OrdinalIgnoreCase));
			_dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
			_dropdown.RefreshShownValue();
			Debug.Log($"[UI_LanguageDropdown] Dropdown option count={_dropdown.options.Count}, values={string.Join(", ", _dropdown.options.Select(o => o.text))}");
		}

		private string GetSavedOrCurrentLocaleCode()
		{
			if (_settingsService != null && !string.IsNullOrEmpty(_settingsService.LocaleCode))
				return _settingsService.LocaleCode;
			return _localizationService != null ? _localizationService.GetCurrentLocaleCode() : (LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en");
		}

		private static string GetDisplayName(Locale locale)
		{
			// 네이티브 이름 우선, 없으면 로캘 이름/코드 폴백
			if (LocaleDisplayOverrides.TryGetValue(locale.Identifier.Code, out var overrideText))
				return overrideText;

			var ci = locale.Identifier.CultureInfo;
			if (ci != null && !string.IsNullOrEmpty(ci.NativeName))
				return ci.NativeName;
			if (!string.IsNullOrEmpty(locale.LocaleName))
				return $"{locale.LocaleName} ({locale.Identifier.Code})";
			return locale.Identifier.Code;
		}

		private async void OnDropdownChanged(int index)
		{
			if (_locales == null || index < 0 || index >= _locales.Count)
				return;

			var code = _locales[index].Identifier.Code;
			_localizationService?.SetLocale(code);
			if (_settingsService != null)
			{
				_settingsService.SetLocaleCode(code);
				await _settingsService.SaveAsync();
			}

			// 선택 직후 라벨/옵션이 즉시 갱신되도록 강제 리프레시
			await BuildOptionsAsync();
			_dropdown.RefreshShownValue();
		}
	}
}


