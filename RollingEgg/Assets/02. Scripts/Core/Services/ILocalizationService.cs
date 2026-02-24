using System;
using Cysharp.Threading.Tasks;

namespace RollingEgg.Core
{
	/// <summary>
	/// 로컬라이제이션 조회 및 로캘 전환을 위한 인터페이스
	/// Unity Localization을 래핑하여 프로젝트 아키텍처(Service Locator)에 통합한다.
	/// </summary>
	public interface ILocalizationService
	{
		/// <summary>
		/// 지정된 테이블/키의 현 로캘 문자열을 비동기로 반환한다.
		/// 필요 시 Smart String 파라미터를 arguments로 전달한다.
		/// </summary>
		/// <param name="table">String Table 이름</param>
		/// <param name="key">엔트리 키</param>
		/// <param name="arguments">Smart String에 바인딩할 인자 목록</param>
		/// <returns>로컬라이즈된 문자열(없으면 빈 문자열 또는 키 반환 정책 적용)</returns>
		UniTask<string> GetAsync(string table, string key, params object[] arguments);

		/// <summary>
		/// 언어 코드를 기반으로 로캘을 전환한다. 예) "ko-KR", "en"
		/// </summary>
		/// <param name="localeCode">로캘 코드</param>
		void SetLocale(string localeCode);

		/// <summary>
		/// 현재 선택된 로캘 코드를 반환한다.
		/// </summary>
		string GetCurrentLocaleCode();

		/// <summary>
		/// 로캘 변경 시 발생하는 이벤트
		/// </summary>
		event Action OnLocaleChanged;
	}
}


