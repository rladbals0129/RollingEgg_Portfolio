using Cysharp.Threading.Tasks;
using UnityEngine; // KeyCode
using RollingEgg; // EPathColor

namespace RollingEgg.Core
{
	/// <summary>
	/// 사용자 환경설정(JSON) 저장/로드 서비스 인터페이스
	/// </summary>
	public interface ISettingsService
	{
		UniTask InitializeAsync();
		UniTask SaveAsync();

		string LocaleCode { get; }
		float PlayerSpeed { get; set; }
		void SetLocaleCode(string code);

		// 러닝 색상 변경 키바인딩 접근자
		KeyCode GetColorKey(EColorKeyType colorKeyType);
		void SetColorKey(EColorKeyType colorKeyType, KeyCode key);
	}
}


