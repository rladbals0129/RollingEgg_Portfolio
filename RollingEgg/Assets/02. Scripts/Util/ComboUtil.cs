using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Util
{
    /// <summary>
    /// 콤보 배율 데이터 구조
    /// </summary>
    [System.Serializable]
    public class ComboRateData
    {
        public int id;
        public int min;
        public int max;
        public int rate; // 배율 %
    }

    /// <summary>
    /// 콤보 관련 유틸리티 클래스
    /// </summary>
    public static class ComboUtil
    {
        /// <summary>
        /// 콤보 배율 테이블
        /// </summary>
        private static readonly List<ComboRateData> _comboRateTable = new List<ComboRateData>
        {
            new ComboRateData { id = 1, min = 1, max = 9, rate = 10 },
            new ComboRateData { id = 2, min = 10, max = 19, rate = 20 },
            new ComboRateData { id = 3, min = 20, max = 29, rate = 30 },
            new ComboRateData { id = 4, min = 30, max = 39, rate = 40 },
            new ComboRateData { id = 5, min = 40, max = 49, rate = 50 },
            new ComboRateData { id = 6, min = 50, max = 59, rate = 60 },
            new ComboRateData { id = 7, min = 60, max = 299, rate = 70 }
        };

        /// <summary>
        /// 콤보 속도 배율 테이블
        /// </summary>
        private static readonly List<ComboRateData> _comboSpeedRateTable = new List<ComboRateData>
        {
            new ComboRateData { id = 1, min = 1, max = 9, rate = 0 },
            new ComboRateData { id = 2, min = 10, max = 19, rate = 50 },
            new ComboRateData { id = 3, min = 20, max = 29, rate = 100 },
            new ComboRateData { id = 4, min = 30, max = 39, rate = 200 },
            new ComboRateData { id = 5, min = 40, max = 49, rate = 300 },
            new ComboRateData { id = 6, min = 50, max = 59, rate = 400 },
            new ComboRateData { id = 7, min = 60, max = 299, rate = 500 }
        };

        /// <summary>
        /// 콤보 개수에 따른 배율(%)을 반환
        /// </summary>
        /// <param name="comboCount">현재 콤보 개수</param>
        /// <returns>배율 (%)</returns>
        public static int GetComboRate(int comboCount)
        {
            foreach (var rateData in _comboRateTable)
            {
                if (comboCount >= rateData.min && comboCount <= rateData.max)
                {
                    return rateData.rate;
                }
            }
            return 0; // 기본값 (콤보가 없거나 범위를 벗어난 경우)
        }

        /// <summary>
        /// 콤보 개수에 따른 속도 배율(%)을 반환
        /// </summary>
        /// <param name="comboCount">현재 콤보 개수</param>
        /// <returns>속도 배율 (%)</returns>
        public static int GetComboSpeedRate(int comboCount)
        {
            foreach (var rateData in _comboSpeedRateTable)
            {
                if (comboCount >= rateData.min && comboCount <= rateData.max)
                {
                    return rateData.rate;
                }
            }
            return 0; // 기본값 (콤보가 없거나 범위를 벗어난 경우)
        }


        /// <summary>
        /// BaseScore에 콤보 배율을 적용한 최종 점수를 계산
        /// </summary>
        /// <param name="baseScore">기본 점수</param>
        /// <param name="comboCount">현재 콤보 개수</param>
        /// <returns>콤보 배율이 적용된 최종 점수 (소수점 버림)</returns>
        /// <example>
        /// baseScore = 5, comboCount = 10 (rate = 40%)
        /// 계산: 5 + (5 * 40 / 100) = 5 + 2.0 = 7.0 → 7
        /// </example>
        public static int CalculateScoreWithCombo(int baseScore, int comboCount)
        {
            if (baseScore <= 0)
                return 0;

            int comboRate = GetComboRate(comboCount);
            float scoreWithCombo = baseScore + (baseScore * comboRate / 100f);
            return Mathf.FloorToInt(scoreWithCombo); // 소수점 버림
        }

        /// <summary>
        /// 기본 속도에 콤보 속도 배율을 적용한 최종 속도를 계산
        /// </summary>
        /// <param name="baseSpeed">기본 속도</param>
        /// <param name="comboCount">현재 콤보 개수</param>
        /// <returns>콤보 속도 배율이 적용된 최종 속도</returns>
        /// <example>
        /// baseSpeed = 5.0, comboCount = 10 (rate = 200%)
        /// 계산: 5.0 + (5.0 * 200 / 100) = 5.0 + 10.0 = 15.0
        /// </example>
        public static float CalculateSpeedWithCombo(float baseSpeed, int comboCount)
        {
            if (baseSpeed <= 0)
                return 0;

            int speedRate = GetComboSpeedRate(comboCount);
            float speedWithCombo = baseSpeed + (baseSpeed * speedRate / 100f);
            return speedWithCombo;
        }
    }
}