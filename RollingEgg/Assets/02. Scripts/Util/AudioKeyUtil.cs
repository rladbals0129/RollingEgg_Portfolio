using RollingEgg.Core;
using System;
using UnityEngine;

namespace RollingEgg
{
    /// <summary>
    /// Audio Key Enum을 Addressable Key 문자열로 변환하는 유틸리티
    /// </summary>
    public static class AudioKeyUtil
    {
        /// <summary>
        /// EBGMKey를 Addressable Key 문자열로 변환
        /// </summary>
        public static string ToAddressableKey(this EBGMKey bgmKey)
        {
            if (bgmKey == EBGMKey.None)
                return string.Empty;

            return bgmKey.ToString();
        }

        /// <summary>
        /// ESFXKey를 Addressable Key 문자열로 변환
        /// </summary>
        public static string ToAddressableKey(this ESFXKey sfxKey)
        {
            if (sfxKey == ESFXKey.None)
                return string.Empty;

            return sfxKey.ToString();
        }

        /// <summary>
        /// 문자열을 EBGMKey로 변환 시도
        /// </summary>
        public static bool TryParseBGMKey(string key, out EBGMKey bgmKey)
        {
            if (string.IsNullOrEmpty(key))
            {
                bgmKey = EBGMKey.None;
                return false;
            }

            return Enum.TryParse(key, out bgmKey);
        }

        /// <summary>
        /// 문자열을 ESFXKey로 변환 시도
        /// </summary>
        public static bool TryParseSFXKey(string key, out ESFXKey sfxKey)
        {
            if (string.IsNullOrEmpty(key))
            {
                sfxKey = ESFXKey.None;
                return false;
            }

            return Enum.TryParse(key, out sfxKey);
        }

        /// <summary>
        /// Chapter ID를 BGM Key로 변환
        /// </summary>
        public static EBGMKey GetBGMKeyByChapterId(int chapterId)
        {
            return chapterId switch
            {
                1 => EBGMKey.BGM_Running_Blue,
                2 => EBGMKey.BGM_Running_Red,
                3 => EBGMKey.BGM_Running_White,
                4 => EBGMKey.BGM_Running_Black,
                5 => EBGMKey.BGM_Running_Yellow,
                _ => EBGMKey.BGM_Running_Blue // 기본값
            };
        }

        /// <summary>
        /// Judgment Type을 SFX Key로 변환
        /// </summary>
        public static ESFXKey GetSFXKeyByJudgment(EJudgmentType judgment)
        {
            return judgment switch
            {
                EJudgmentType.PERFECT => ESFXKey.SFX_Judgment_Perfect,
                EJudgmentType.GREAT => ESFXKey.SFX_Judgment_Great,
                EJudgmentType.GOOD => ESFXKey.SFX_Judgment_Good,
                EJudgmentType.BAD => ESFXKey.SFX_Judgment_Bad,
                EJudgmentType.MISS => ESFXKey.SFX_Judgment_Miss,
                _ => ESFXKey.None
            };
        }
    }
}
