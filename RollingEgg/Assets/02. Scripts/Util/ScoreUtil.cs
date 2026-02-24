using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Util
{
    public enum EClearRank
    {
        F,   // 0~49점
        E,   // 50~79점
        D,   // 80~109점
        C,   // 110~139점
        B,   // 140~169점
        A,   // 170~199점
        S,   // 200~249점
        SS   // 250점 이상
    }

    /// <summary>
    /// 점수 및 등급 관련 유틸리티 클래스
    /// </summary>
    public static class ScoreUtil
    {
        /// <summary>
        /// Judgment 타입별 기본 점수
        /// </summary>
        private static readonly Dictionary<EJudgmentType, int> BASE_SCORES = new Dictionary<EJudgmentType, int>
        {
            { EJudgmentType.MISS, 0 },
            { EJudgmentType.BAD, 1 },
            { EJudgmentType.GOOD, 2 },
            { EJudgmentType.GREAT, 3 },
            { EJudgmentType.PERFECT, 5 }
        };

        /// <summary>
        /// 등급별 최소 점수 테이블
        /// </summary>
        private static readonly Dictionary<EClearRank, int> RANK_MIN_SCORES = new Dictionary<EClearRank, int>
        {
            { EClearRank.SS, 250 },
            { EClearRank.S, 200 },
            { EClearRank.A, 170 },
            { EClearRank.B, 140 },
            { EClearRank.C, 110 },
            { EClearRank.D, 80 },
            { EClearRank.E, 50 },
            { EClearRank.F, 0 }
        };

        /// <summary>
        /// 등급별 보상 테이블
        /// </summary>
        private static readonly Dictionary<EClearRank, int> RANK_REWARDS = new Dictionary<EClearRank, int>
        {
            { EClearRank.F, 50 },
            { EClearRank.E, 100 },
            { EClearRank.D, 150 },
            { EClearRank.C, 200 },
            { EClearRank.B, 250 },
            { EClearRank.A, 300 },
            { EClearRank.S, 400 },
            { EClearRank.SS, 500 }
        };

        /// <summary>
        /// Judgment 타입에 따른 기본 점수를 반환
        /// </summary>
        /// <param name="judgment">판정 타입</param>
        /// <returns>기본 점수</returns>
        public static int GetBaseScore(EJudgmentType judgment)
        {
            return BASE_SCORES.TryGetValue(judgment, out int score) ? score : 0;
        }

        /// <summary>
        /// 점수에 따라 클리어 등급을 반환
        /// </summary>
        /// <param name="score">총 점수</param>
        /// <returns>클리어 등급</returns>
        public static EClearRank GetRankByScore(int score)
        {
            // 높은 등급부터 확인 (SS -> F 순서)
            foreach (var rank in new[] { EClearRank.SS, EClearRank.S, EClearRank.A, EClearRank.B,
                                         EClearRank.C, EClearRank.D, EClearRank.E })
            {
                if (score >= RANK_MIN_SCORES[rank])
                    return rank;
            }
            return EClearRank.F;
        }

        /// <summary>
        /// 등급에 따라 보상 금액을 반환
        /// </summary>
        /// <param name="rank">클리어 등급</param>
        /// <returns>보상 금액</returns>
        public static int GetRewardByRank(EClearRank rank)
        {
            return RANK_REWARDS.TryGetValue(rank, out int reward) ? reward : 0;
        }

        /// <summary>
        /// 등급의 최소 점수를 반환
        /// </summary>
        /// <param name="rank">클리어 등급</param>
        /// <returns>최소 점수</returns>
        public static int GetMinScoreByRank(EClearRank rank)
        {
            return RANK_MIN_SCORES.TryGetValue(rank, out int score) ? score : 0;
        }

        // ScoreUtil.cs에 추가
        /// <summary>
        /// HP 비율에 따른 점수 계산
        /// </summary>
        public static int CalculateHPScore(float currentHP, float maxHP)
        {
            float hpRatio = maxHP > 0f ? currentHP / maxHP : 0f;
            return Mathf.RoundToInt(hpRatio * 100f);
        }
    }
}