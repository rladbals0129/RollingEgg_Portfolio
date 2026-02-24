using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RollingEgg.Data;
using RollingEgg.Util;
using UnityEngine;

namespace RollingEgg.Core
{
    /// <summary>
    /// 스테이지 데이터 및 진행도를 관리하는 서비스 인터페이스
    /// </summary>
    public interface IStageService
    {
        UniTask InitializeAsync();

        IReadOnlyList<StageTableSO.StageRow> GetStagesByChapter(int chapterId);
        bool TryGetStage(int stageId, out StageTableSO.StageRow row);
        StageTableSO.StageRow GetStage(int stageId);

        StageProgressData GetProgress(int stageId);
        bool IsStageUnlocked(int stageId);

        void UpdateStageResult(int stageId, int score, EClearRank rank);

        int GetLastSelectedChapter();
        int GetLastSelectedStage();
        void SetLastSelection(int chapterId, int stageId);

        UniTask SaveDataAsync();
        UniTask LoadDataAsync();
    }

    /// <summary>
    /// 스테이지 진행도 정보
    /// </summary>
    public readonly struct StageProgressData
    {
        public StageProgressData(bool isCleared, EClearRank bestRank, int bestScore, long lastPlayedTicks)
        {
            IsCleared = isCleared;
            BestRank = bestRank;
            BestScore = bestScore;
            LastPlayedTicks = lastPlayedTicks;
        }

        public bool IsCleared { get; }
        public EClearRank BestRank { get; }
        public int BestScore { get; }
        public long LastPlayedTicks { get; }

        public static StageProgressData Default => new StageProgressData(false, EClearRank.F, 0, 0);
    }
}


