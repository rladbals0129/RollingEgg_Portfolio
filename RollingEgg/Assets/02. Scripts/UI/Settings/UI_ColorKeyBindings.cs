using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RollingEgg.Core;
using RollingEgg.Util;

namespace RollingEgg.UI
{
	/// <summary>
	/// 설정 화면에서 러닝 색상 변경 키(5개)를 리바인드하는 UI 컴포넌트
	/// - 버튼 클릭 후 다음 키 입력을 감지하여 저장
	/// - 저장은 즉시 JSON(UserSettings)으로 반영
	/// </summary>
	public sealed class UI_ColorKeyBindings : MonoBehaviour
	{
		[Serializable]
		private sealed class BindingEntry
		{
			public EColorKeyType colorKeyType;
			public Button rebindButton;
			public TMP_Text keyLabel;
		}

		[SerializeField] private List<BindingEntry> _entries = new List<BindingEntry>();

		private ISettingsService _settingsService;
		private bool _isRebinding;
		private EColorKeyType _rebindingType;

		// 허용 키 집합(일반적으로 사용하는 키들 위주)
		private static readonly KeyCode[] _allowedKeys = KeyCodeUtil.BuildAllowedKeys();

		private void OnEnable()
		{
			_settingsService = ServiceLocator.Get<ISettingsService>();
			BindButtons();
			RefreshAllLabels();
		}

		private void OnDisable()
		{
			UnbindButtons();
			_isRebinding = false;
		}

		private void Update()
		{
			if (!_isRebinding)
				return;

			if (TryDetectPressedKeyDown(out var pressed))
			{
				_settingsService.SetColorKey(_rebindingType, pressed);
				SaveAsync().Forget();
				_isRebinding = false;
				RefreshAllLabels();
			}
		}

		private void BindButtons()
		{
			foreach (var entry in _entries)
			{
				if (entry?.rebindButton == null)
					continue;

				var localEntry = entry; // capture
				entry.rebindButton.onClick.AddListener(() => StartRebind(localEntry.colorKeyType, localEntry.keyLabel));
			}
		}

		private void UnbindButtons()
		{
			foreach (var entry in _entries)
			{
				if (entry?.rebindButton == null)
					continue;
				entry.rebindButton.onClick.RemoveAllListeners();
			}
		}

		private void StartRebind(EColorKeyType colorKeyType, TMP_Text label)
		{
			_rebindingType = colorKeyType;
			_isRebinding = true;
			if (label != null)
				label.text = "Press any key...";
		}

		private void RefreshAllLabels()
		{
			if (_settingsService == null)
				return;

			foreach (var entry in _entries)
			{
				if (entry?.keyLabel == null)
					continue;
				var key = _settingsService.GetColorKey(entry.colorKeyType);
				entry.keyLabel.text = KeyToDisplay(key);
			}
		}

		private static string KeyToDisplay(KeyCode key)
		{
			return key == KeyCode.None ? "None" : KeyCodeUtil.GetDisplayString(key);
		}

		private async UniTaskVoid SaveAsync()
		{
			if (_settingsService != null)
				await _settingsService.SaveAsync();
		}

		private static bool TryDetectPressedKeyDown(out KeyCode pressed)
		{
			pressed = KeyCode.None;
			if (!Input.anyKeyDown)
				return false;

			for (int i = 0; i < _allowedKeys.Length; i++)
			{
				var key = _allowedKeys[i];
				if (Input.GetKeyDown(key))
				{
					pressed = key;
					return true;
				}
			}
			return false;
		}
	}
}


