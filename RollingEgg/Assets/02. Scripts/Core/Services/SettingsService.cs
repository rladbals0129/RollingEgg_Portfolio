using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RollingEgg; // for EPathColor

namespace RollingEgg.Core
{
	[Serializable]
	public class UserSettings
	{
		public string localeCode = "ko-KR";
		// 향후: 오디오/그래픽/키바인딩 등 추가 필드
		// 러닝 색상 변경 키바인딩 (기본: Q,W,E,R,T)
		public KeyBindings keyBindings = new KeyBindings();

		// 플레이어 속도 (임시 디버깅용)
		public float playerSpeed = 5f;
	}

	[Serializable]
	public class KeyBindings
	{
		public KeyCode key1 = KeyCode.S;
		public KeyCode key2 = KeyCode.D;
		public KeyCode key3 = KeyCode.F;
		public KeyCode key4 = KeyCode.J;
		public KeyCode key5 = KeyCode.K;
		public KeyCode key6 = KeyCode.L;

		public KeyCode Get(EColorKeyType colorKeyType)
		{
			switch (colorKeyType)
			{
				case EColorKeyType.S: return key1;
				case EColorKeyType.D: return key2;
				case EColorKeyType.F: return key3;
				case EColorKeyType.J: return key4;
				case EColorKeyType.K: return key5;
				case EColorKeyType.L: return key6;
				default: return KeyCode.None;
			}
		}

		public void Set(EColorKeyType colorKeyType, KeyCode key)
		{
			// 이미 다른 색상에 할당된 키라면 서로 스왑하여 일관성 유지
			var currentKey = Get(colorKeyType);
			if (currentKey == key)
				return;

			var usedBy = FindColorByKey(key);
			if (usedBy != EColorKeyType.None && usedBy != colorKeyType)
			{
				// swap: target <- key, usedBy <- currentKey
				SetDirect(colorKeyType, key);
				SetDirect(usedBy, currentKey);
			}
			else
			{
				SetDirect(colorKeyType, key);
			}
		}

		private void SetDirect(EColorKeyType colorKeyType, KeyCode key)
		{
			switch (colorKeyType)
			{
				case EColorKeyType.S: key1 = key; break;
				case EColorKeyType.D: key2 = key; break;
				case EColorKeyType.F: key3 = key; break;
				case EColorKeyType.J: key4 = key; break;
				case EColorKeyType.K: key5 = key; break;
				case EColorKeyType.L: key6 = key; break;
				default: break;
			}
		}

		private EColorKeyType FindColorByKey(KeyCode key)
		{
			if (key1 == key) return EColorKeyType.S;
			if (key2 == key) return EColorKeyType.D;
			if (key3 == key) return EColorKeyType.F;
			if (key4 == key) return EColorKeyType.J;
			if (key5 == key) return EColorKeyType.K;
			if (key6 == key) return EColorKeyType.L;
			return EColorKeyType.None;
		}
	}

	/// <summary>
	/// 사용자 환경설정을 JSON으로 로드/저장하는 서비스 구현
	/// </summary>
	public class SettingsService : ISettingsService
	{
		private const string FileName = "UserSettings.json";
		private string _path;
		private UserSettings _data = new UserSettings();

		public string LocaleCode => _data.localeCode;
		public KeyCode GetColorKey(EColorKeyType colorKeyType) => _data.keyBindings != null ? _data.keyBindings.Get(colorKeyType) : KeyCode.None;
		public void SetColorKey(EColorKeyType colorKeyType, KeyCode key)
		{
			if (_data.keyBindings == null)
				_data.keyBindings = new KeyBindings();
			_data.keyBindings.Set(colorKeyType, key);
		}

		public float PlayerSpeed { get => _data.playerSpeed; set => _data.playerSpeed = value; }

		public async UniTask InitializeAsync()
		{
			_path = Path.Combine(Application.persistentDataPath, FileName);
			try
			{
				if (File.Exists(_path))
				{
					var json = await File.ReadAllTextAsync(_path);
					var loaded = JsonUtility.FromJson<UserSettings>(json);
					if (loaded != null) _data = loaded;
					// 새 필드가 없거나 기본값이면 기본 키 설정 보장 (qwert)
					if (_data.keyBindings == null)
						_data.keyBindings = new KeyBindings();
				}
				else
				{
					await SaveAsync();
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[SettingsService] Initialize failed: {e.Message}");
			}
		}

		public void SetLocaleCode(string code)
		{
			if (!string.IsNullOrEmpty(code))
				_data.localeCode = code;
		}

		public async UniTask SaveAsync()
		{
			try
			{
				var json = JsonUtility.ToJson(_data, true);
				await File.WriteAllTextAsync(_path, json);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[SettingsService] Save failed: {e.Message}");
			}
		}
	}
}


