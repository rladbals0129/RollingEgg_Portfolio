using RollingEgg.Core;
using TMPro;
using UnityEngine;

namespace RollingEgg.UI
{
    /// <summary>
    /// 현재 로캘 코드에 따라 TMP_Text 폰트를 교체해 주는 컴포넌트
    /// - \"Korean (South Korea)\"  : ko-KR  -> 한국어/영어 폰트(DNFBitBitv2 등)
    /// - \"English\"               : en     -> 한국어/영어 폰트(DNFBitBitv2 등)
    /// - \"Chinese (Traditional)\" : zh-Hant -> CJK 폰트(BoutiqueBitmap9x9_Bold_2 등)
    /// - \"Chinese (Simplified)\"  : zh-Hans -> CJK 폰트
    /// - \"Japanese\"              : ja-JP   -> CJK 폰트
    /// 필요 시 인스펙터에서 폰트만 교체하여 사용한다.
    /// </summary>
    public class LocalizedTMPFont : MonoBehaviour
    {
        [Header("폰트 설정")]
        [SerializeField] private TMP_FontAsset koreanEnglishFont;    // 예: DNFBitBitv2 SDF
        [SerializeField] private TMP_FontAsset cjkFont;              // 예: BoutiqueBitmap9x9_Bold_2

        private TMP_Text _text;
        private ILocalizationService _localizationService;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();

            if (!ServiceLocator.HasService<ILocalizationService>())
            {
                Debug.LogWarning("[LocalizedTMPFont] ILocalizationService가 등록되어 있지 않습니다.");
                return;
            }

            _localizationService = ServiceLocator.Get<ILocalizationService>();
            if (_localizationService == null)
            {
                Debug.LogWarning("[LocalizedTMPFont] ILocalizationService 인스턴스를 가져오지 못했습니다.");
                return;
            }

            // 초기 로캘에 맞춰 폰트 적용
            ApplyCurrentLocaleFont();

            // 로캘 변경 이벤트 구독
            _localizationService.OnLocaleChanged += HandleLocaleChanged;
        }

        private void OnDestroy()
        {
            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            }
        }

        private void HandleLocaleChanged()
        {
            ApplyCurrentLocaleFont();
        }

        /// <summary>
        /// 현재 로캘 코드에 따라 적절한 폰트를 적용한다.
        /// </summary>
        private void ApplyCurrentLocaleFont()
        {
            if (_localizationService == null || _text == null)
                return;

            string code = _localizationService.GetCurrentLocaleCode();
            if (string.IsNullOrEmpty(code))
                return;

            TMP_FontAsset targetFont = GetFontForLocale(code);
            if (targetFont != null && _text.font != targetFont)
            {
                _text.font = targetFont;
            }
        }

        /// <summary>
        /// 로캘 코드에 따라 사용할 폰트를 결정한다.
        /// - ko-KR, en          => koreanEnglishFont
        /// - zh-Hant, zh-Hans,
        ///   ja-JP              => cjkFont
        /// 필요하면 조건을 프로젝트 로캘 코드에 맞게 확장/수정한다.
        /// </summary>
        private TMP_FontAsset GetFontForLocale(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode))
                return _text.font;

            // Unity Localization에서 넘어오는 코드 형식:
            // - Korean (South Korea) : ko-KR
            // - English              : en
            // - Chinese (Traditional): zh-Hant
            // - Chinese (Simplified) : zh-Hans
            // - Japanese             : ja-JP

            switch (localeCode)
            {
                case "ko-KR":
                case "en":
                    return koreanEnglishFont != null ? koreanEnglishFont : _text.font;

                case "zh-Hant":
                case "zh-Hans":
                case "ja-JP":
                    return cjkFont != null ? cjkFont : _text.font;

                default:
                    // 예상치 못한 코드(예: en-US 등)는 앞 2글자로 한 번 더 폴백
                    var lower = localeCode.ToLowerInvariant();
                    if (lower.StartsWith("ko") || lower.StartsWith("en"))
                        return koreanEnglishFont != null ? koreanEnglishFont : _text.font;
                    if (lower.StartsWith("zh") || lower.StartsWith("ja"))
                        return cjkFont != null ? cjkFont : _text.font;
                    return _text.font;
            }
        }
    }
}
