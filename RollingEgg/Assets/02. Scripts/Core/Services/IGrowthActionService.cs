using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RollingEgg.Data;

namespace RollingEgg.Core
{
    /// <summary>
    /// 육성 행동 관리 서비스 인터페이스
    /// 육성 행동 수행, 스탯 관리, 조건 선택 기능을 제공
    /// </summary>
    public interface IGrowthActionService
    {
        /// <summary>
        /// 서비스 초기화
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// 특정 알의 현재 스탯 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>스탯 배열 [Gentle, Active, Cautious, Love, Chaos]</returns>
        int[] GetEggStats(int eggId);

        /// <summary>
        /// 특정 스탯 값 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="statType">스탯 타입</param>
        /// <returns>스탯 값</returns>
        int GetStatValue(int eggId, StatType statType);

        /// <summary>
        /// 육성 행동 수행
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="actionId">행동 ID</param>
        /// <returns>수행 결과</returns>
        UniTask<GrowthActionResult> PerformActionAsync(int eggId, int actionId);

        /// <summary>
        /// 알에 대한 가능한 육성 행동 목록 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggLevel">알 레벨</param>
        /// <param name="count">선택할 행동 개수 (기본 5개)</param>
        /// <returns>선택된 육성 행동 목록</returns>
        List<GrowthConditionPoolSO.ConditionEntry> GetAvailableActions(int eggId, int eggLevel, int count = 5);

        /// <summary>
        /// 육성 행동 가능 여부 확인
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="actionId">행동 ID</param>
        /// <returns>가능 여부</returns>
        bool CanPerformAction(int eggId, int actionId);

        /// <summary>
        /// 알의 총 육성 행동 횟수 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>총 행동 횟수</returns>
        int GetTotalActionCount(int eggId);

        /// <summary>
        /// 스탯 증가
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="statType">스탯 타입</param>
        /// <param name="amount">증가량</param>
        /// <param name="source">증가 출처</param>
        void IncreaseStat(int eggId, StatType statType, int amount, string source = "");

        /// <summary>
        /// 스탯 설정
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="statType">스탯 타입</param>
        /// <param name="value">설정할 값</param>
        void SetStat(int eggId, StatType statType, int value);

        /// <summary>
        /// 알의 모든 스탯 초기화 (진화 완료 후)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        void ResetEggStats(int eggId);

        /// <summary>
        /// 알 생성 (새로운 알 데이터 초기화)
        /// </summary>
        /// <param name="eggId">알 ID (EggTableSO에서 조회)</param>
        void CreateEgg(int eggId);
        
        /// <summary>
        /// 알 정보 조회 (EggTableSO에서)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>알 정보 (없으면 null)</returns>
        EggTableSO.EggRow GetEggInfo(int eggId);

        /// <summary>
        /// 알 삭제
        /// </summary>
        /// <param name="eggId">알 ID</param>
        void RemoveEgg(int eggId);

        /// <summary>
        /// 알 존재 여부 확인
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>존재 여부</returns>
        bool HasEgg(int eggId);

        /// <summary>
        /// 모든 알 ID 목록 조회
        /// </summary>
        /// <returns>알 ID 배열</returns>
        int[] GetAllEggIds();

        /// <summary>
        /// 알의 육성 행동 기록 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>행동 기록 목록</returns>
        List<GrowthActionRecord> GetActionHistory(int eggId);

        /// <summary>
        /// 알 타입 조회 (예: yellow, blue, black, red, white)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>알 타입 문자열. 없으면 빈 문자열</returns>
        string GetEggType(int eggId);

        /// <summary>
        /// 스탯 타입별 메타데이터 조회 (색상, 표시명 등)
        /// </summary>
        /// <param name="statType">스탯 타입</param>
        /// <returns>스탯 정보. 없으면 null</returns>
        StatTypeSO.StatInfo GetStatInfo(StatType statType);

        /// <summary>
        /// StatTypeSO 전체 조회 (필요 시)
        /// </summary>
        /// <returns>StatTypeSO 인스턴스</returns>
        StatTypeSO GetStatTypeSO();

        /// <summary>
        /// 최근 육성 행동 기록 (쿨다운 관리용)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>최근 행동 ID 목록</returns>
        List<int> GetRecentActionIds(int eggId);

        /// <summary>
        /// 데이터 저장
        /// </summary>
        UniTask SaveDataAsync();

        /// <summary>
        /// 데이터 로드
        /// </summary>
        UniTask LoadDataAsync();

        /// <summary>
        /// 스탯 변경 이벤트
        /// </summary>
        event Action<StatChangedEvent> OnStatChanged;

        /// <summary>
        /// 육성 행동 수행 이벤트
        /// </summary>
        event Action<GrowthActionPerformedEvent> OnActionPerformed;

        /// <summary>
        /// 레벨 변경 이벤트
        /// </summary>
        event Action<LevelChangedEvent> OnLevelChanged;

        /// <summary>
        /// 알 레벨 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>현재 레벨</returns>
        int GetEggLevel(int eggId);

        /// <summary>
        /// 알 레벨 설정
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="level">설정할 레벨</param>
        void SetEggLevel(int eggId, int level);
    }

    /// <summary>
    /// 육성 행동 수행 결과
    /// </summary>
    public struct GrowthActionResult
    {
        public bool success;                    // 성공 여부
        public string errorMessage;             // 에러 메시지 (실패 시)
        public int[] statChanges;               // 변경된 스탯 배열
        public int totalActionCount;            // 총 행동 횟수
        public GrowthConditionPoolSO.ConditionEntry action; // 수행된 행동 정보
    }

    /// <summary>
    /// 육성 행동 기록
    /// </summary>
    public struct GrowthActionRecord
    {
        public int actionId;                    // 행동 ID
        public string actionName;               // 행동 이름
        public int[] statChanges;               // 스탯 변경량
        public long timestamp;                  // 수행 시간 (Unix timestamp)
        public int sessionId;                   // 세션 ID
    }
}
