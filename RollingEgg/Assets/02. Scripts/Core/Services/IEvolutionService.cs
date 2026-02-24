using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RollingEgg.Data;

namespace RollingEgg.Core
{
    /// <summary>
    /// 진화 관리 서비스 인터페이스
    /// 진화 조건 판정, 진화 처리, 결과 결정 기능을 제공
    /// </summary>
    public interface IEvolutionService
    {
        /// <summary>
        /// 서비스 초기화
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// 알의 진화 가능 여부 확인
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="nurtureLevel">현재 육성 레벨</param>
        /// <returns>진화 가능 여부 및 조건 정보</returns>
        EvolutionCondition CheckEvolutionCondition(int eggId, int nurtureLevel);

        /// <summary>
        /// 진화 시도
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="nurtureLevel">현재 육성 레벨</param>
        /// <param name="currentStats">현재 스탯 배열</param>
        /// <returns>진화 결과</returns>
        UniTask<EvolutionResult> AttemptEvolutionAsync(int eggId, int nurtureLevel, int[] currentStats);

        /// <summary>
        /// 스탯에 따른 진화 형태 결정
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggType">알 타입</param>
        /// <param name="nurtureLevel">육성 레벨</param>
        /// <param name="finalStats">최종 스탯 배열</param>
        /// <returns>진화 형태 ID 및 정보</returns>
        EvolutionForm DetermineEvolutionForm(int eggId, string eggType, int nurtureLevel, int[] finalStats);

        /// <summary>
        /// 진화 확률 계산
        /// </summary>
        /// <param name="statType">주요 스탯 타입</param>
        /// <param name="statValue">스탯 값</param>
        /// <param name="otherStats">다른 스탯들의 가중치</param>
        /// <returns>진화 확률 (0.0 ~ 1.0)</returns>
        float CalculateEvolutionProbability(StatType statType, int statValue, 
            Dictionary<StatType, int> otherStats);

        /// <summary>
        /// 알의 현재 진화 단계 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>현재 진화 단계 (0: 미진화, 1: 1단계, 2: 2단계, ...)</returns>
        int GetCurrentEvolutionStage(int eggId);

        /// <summary>
        /// 알의 진화 이력 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <returns>진화 이력 목록</returns>
        List<EvolutionRecord> GetEvolutionHistory(int eggId);

        /// <summary>
        /// 특정 진화 형태 정보 조회
        /// </summary>
        /// <param name="formId">진화 형태 ID</param>
        /// <returns>진화 형태 정보</returns>
        EvolutionForm GetEvolutionFormInfo(int formId);

        /// <summary>
        /// 알 타입별 가능한 진화 형태 목록 조회
        /// </summary>
        /// <param name="eggType">알 타입</param>
        /// <returns>가능한 진화 형태 목록</returns>
        List<EvolutionForm> GetAvailableEvolutionForms(string eggType);

        /// <summary>
        /// 진화 조건 설정 (알별로 다른 조건 적용 가능)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="requiredLevel">필요한 최소 육성 레벨</param>
        void SetEvolutionRequirement(int eggId, int requiredLevel);

        /// <summary>
        /// 진화 완료 후 알 데이터 초기화
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="newFormId">새로운 진화 형태 ID</param>
        void ResetEggAfterEvolution(int eggId, int newFormId);

        /// <summary>
        /// 진화 시뮬레이션 (테스트용)
        /// </summary>
        /// <param name="eggType">알 타입</param>
        /// <param name="stats">스탯 배열</param>
        /// <param name="simulationCount">시뮬레이션 횟수</param>
        /// <returns>진화 결과 통계</returns>
        EvolutionSimulationResult SimulateEvolution(string eggType, int[] stats, int simulationCount = 1000);

        /// <summary>
        /// 예상 진화 결과 가져오기 (진화 확인 팝업용)
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggType">알 타입</param>
        /// <param name="nurtureLevel">현재 육성 레벨</param>
        /// <param name="currentStats">현재 스탯 배열</param>
        /// <returns>예상 진화 결과 목록 (최대 3종)</returns>
        List<PredictedEvolutionOutcome> GetPredictedEvolutionOutcomes(int eggId, string eggType, int nurtureLevel, int[] currentStats);

        /// <summary>
        /// 데이터 저장
        /// </summary>
        UniTask SaveDataAsync();

        /// <summary>
        /// 데이터 로드
        /// </summary>
        UniTask LoadDataAsync();

        /// <summary>
        /// 진화 시도 이벤트
        /// </summary>
        event Action<EvolutionAttemptEvent> OnEvolutionAttempted;

        /// <summary>
        /// 진화 완료 이벤트
        /// </summary>
        event Action<EvolutionCompletedEvent> OnEvolutionCompleted;

        /// <summary>
        /// 진화 실패 이벤트
        /// </summary>
        event Action<EvolutionFailedEvent> OnEvolutionFailed;
    }

    /// <summary>
    /// 진화 조건 정보
    /// </summary>
    public struct EvolutionCondition
    {
        public bool canEvolve;              // 진화 가능 여부
        public bool hasEnoughLevel;         // 레벨 충족 여부
        public int requiredLevel;           // 필요한 육성 레벨
        public int currentLevel;            // 현재 육성 레벨
        public string errorMessage;         // 에러 메시지 (진화 불가능 시)
    }

    /// <summary>
    /// 진화 결과
    /// </summary>
    public struct EvolutionResult
    {
        public bool success;                // 진화 성공 여부
        public int evolutionFormId;         // 진화된 형태 ID
        public string evolutionName;        // 진화된 형태 이름
        public int[] finalStats;            // 최종 스탯 배열
        public float evolutionTime;         // 진화 소요 시간
        public string errorMessage;         // 에러 메시지 (실패 시)
        public EvolutionForm formInfo;      // 진화 형태 정보
        public bool isNewForm;              // 신규 도감 등록 여부
        public int duplicateReward;         // 중복 시 지급된 재화량
    }

    /// <summary>
    /// 진화 형태 정보
    /// </summary>
    public struct EvolutionForm
    {
        public int formId;                  // 형태 ID
        public string formName;             // 형태 이름 (로컬라이즈 키)
        public string eggType;              // 알 타입
        public StatType primaryStat; // 주요 스탯
        public int requiredStatValue;       // 필요한 스탯 값
        public float probability;           // 기본 진화 확률
        public string description;          // 설명 (로컬라이즈 키)
        public string iconAddress;          // 아이콘 주소 (Addressables)
        public string[] unlockConditions;   // 해금 조건 (로컬라이즈 키)
        public EvolvedFormTableSO.BuffType buffType; // 도감 버프 타입
        public float buffValuePercent;      // 버프 수치(%)
        public string buffTargetEggType;    // 버프 대상 알 타입
    }

    /// <summary>
    /// 진화 기록
    /// </summary>
    public struct EvolutionRecord
    {
        public int evolutionFormId;         // 진화 형태 ID
        public string evolutionName;        // 진화 형태 이름
        public int[] statsAtEvolution;      // 진화 당시 스탯
        public long evolutionTime;          // 진화 시간 (Unix timestamp)
        public int sessionId;               // 세션 ID
        public float evolutionDuration;     // 진화 소요 시간
    }

    /// <summary>
    /// 진화 시뮬레이션 결과
    /// </summary>
    public struct EvolutionSimulationResult
    {
        public int totalSimulations;        // 총 시뮬레이션 횟수
        public Dictionary<int, int> formResults; // 형태별 진화 횟수
        public Dictionary<StatType, float> statProbabilities; // 스탯별 확률
        public float averageEvolutionTime;  // 평균 진화 시간
        public string mostCommonForm;       // 가장 흔한 진화 형태
    }

    /// <summary>
    /// 예상 진화 결과 (진화 확인 팝업용)
    /// </summary>
    public struct PredictedEvolutionOutcome
    {
        public int formId;                  // 진화체 ID
        public string formName;             // 진화체 이름 (로컬라이즈 키)
        public float probabilityPercent;    // 확률 (%)
        public string iconAddress;          // 아이콘 주소 (Addressables)
    }
}
