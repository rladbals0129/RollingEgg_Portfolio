using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace RollingEgg.Core
{
	/// <summary>
	/// Unity Localization을 래핑한 구현체
	/// - 비동기 문자열 조회
	/// - 런타임 로캘 전환 및 변경 이벤트 브리징
	/// </summary>
	public class UnityLocalizationService : ILocalizationService
	{
		public event Action OnLocaleChanged;

		public UnityLocalizationService()
		{
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			try { OnLocaleChanged?.Invoke(); }
			catch (Exception e) { Debug.LogWarning($"[UnityLocalizationService] OnLocaleChanged error: {e.Message}"); }
		}

		public async UniTask<string> GetAsync(string table, string key, params object[] arguments)
		{
			if (string.IsNullOrEmpty(key))
				return string.Empty;

			try
			{
				var handle = (arguments != null && arguments.Length > 0)
					? LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key, arguments: arguments)
					: LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
				return await handle.Task;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UnityLocalizationService] GetAsync failed: table={table}, key={key}, err={e.Message}");
				return key; // 폴백: 키를 그대로 반환
			}
		}

		public void SetLocale(string localeCode)
		{
			if (string.IsNullOrEmpty(localeCode)) return;

			var locales = LocalizationSettings.AvailableLocales?.Locales;
			if (locales == null || locales.Count == 0)
			{
				Debug.LogWarning("[UnityLocalizationService] No available locales.");
				return;
			}

			var target = locales.FirstOrDefault(l =>
				string.Equals(l.Identifier.Code, localeCode, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(l.Identifier.CultureInfo?.Name, localeCode, StringComparison.OrdinalIgnoreCase));

			if (target == null)
			{
				Debug.LogWarning($"[UnityLocalizationService] Locale not found: {localeCode}");
				return;
			}

			LocalizationSettings.SelectedLocale = target;
		}

		public string GetCurrentLocaleCode()
		{
			var sel = LocalizationSettings.SelectedLocale;
			return sel != null ? sel.Identifier.Code : string.Empty;
		}
	}
}


