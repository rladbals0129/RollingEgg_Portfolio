using System;
using System.Collections.Generic;
using RollingEgg;
using RollingEgg.Util;
using UnityEngine;

namespace RollingEgg.Core
{
    /// <summary>
    /// 육성 시스템에서 사용되는 모든 이벤트들을 정의
    /// </summary>
    
    // ==================== 재화 관련 이벤트 ====================
    
    /// <summary>
    /// 재화 획득 이벤트
    /// </summary>
    [Serializable]
    public struct CurrencyGainedEvent
    {
        public int currencyId;        // 재화 ID (CurrencyTableSO의 id)
        public int amount;            // 획득량
        public string source;         // 획득 출처 (예: "running_game", "evolution_bonus")
        public int eggId;             // 관련 알 ID (전용 재화의 경우)
    }

    /// <summary>
    /// 재화 소비 이벤트
    /// </summary>
    [Serializable]
    public struct CurrencySpentEvent
    {
        public int currencyId;        // 재화 ID
        public int amount;            // 소비량
        public string purpose;        // 소비 목적 (예: "growth_action", "evolution")
        public int eggId;             // 관련 알 ID
    }

    /// <summary>
    /// 재화 잔액 변경 이벤트
    /// </summary>
    [Serializable]
    public struct CurrencyBalanceChangedEvent
    {
        public int currencyId;        // 재화 ID
        public int oldAmount;         // 이전 잔액
        public int newAmount;         // 현재 잔액
        public int changeAmount;      // 변경량 (+/-)
    }

   
    // ==================== 육성 행동 관련 이벤트 ====================
    
    /// <summary>
    /// 육성 행동 수행 이벤트
    /// </summary>
    [Serializable]
    public struct GrowthActionPerformedEvent
    {
        public int eggId;             // 알 ID
        public int actionId;          // 행동 ID (GrowthConditionPoolSO의 id)
        public int costCommon;        // 소비한 공용 재화
        public int costSpecial;       // 소비한 전용 재화
        public string actionName;     // 행동 이름 (로컬라이즈 키)
    }

    /// <summary>
    /// 스탯 변경 이벤트
    /// </summary>
    [Serializable]
    public struct StatChangedEvent
    {
        public int eggId;             // 알 ID
        public int statType;          // 스탯 타입 (GrowthConditionPoolSO.StatType)
        public int oldValue;          // 이전 스탯 값
        public int newValue;          // 현재 스탯 값
        public int changeAmount;      // 변경량 (+/-)
        public string statName;       // 스탯 이름 (로컬라이즈 키)
    }

    /// <summary>
    /// 레벨 변경 이벤트
    /// </summary>
    [Serializable]
    public struct LevelChangedEvent
    {
        public int eggId;             // 알 ID
        public int oldLevel;          // 이전 레벨
        public int newLevel;          // 현재 레벨
        public int changeAmount;      // 변경량 (+/-)
        public string changeReason;   // 변경 이유 (예: "growth_action", "evolution_reset")
    }

    // ==================== 진화 관련 이벤트 ====================
    
    /// <summary>
    /// 진화 시도 이벤트
    /// </summary>
    [Serializable]
    public struct EvolutionAttemptEvent
    {
        public int eggId;             // 알 ID
        public int nurtureLevel;      // 현재 육성 레벨
        public int requiredLevel;     // 필요한 최소 육성 레벨
        public bool canEvolve;        // 진화 가능 여부
    }

    /// <summary>
    /// 진화 완료 이벤트
    /// </summary>
    [Serializable]
    public struct EvolutionCompletedEvent
    {
        public int eggId;             // 알 ID
        public int evolutionFormId;   // 진화된 형태 ID
        public string evolutionName;  // 진화된 형태 이름 (로컬라이즈 키)
        public int[] finalStats;      // 최종 스탯 배열 [온순함, 활발함, 신중함, 대담함, 예술적]
        public float evolutionTime;   // 진화 소요 시간
    }

    /// <summary>
    /// 진화 실패 이벤트 (조건 미충족)
    /// </summary>
    [Serializable]
    public struct EvolutionFailedEvent
    {
        public int eggId;             // 알 ID
        public string reason;         // 실패 이유 (로컬라이즈 키)
        public int missingLevel;      // 부족한 육성 레벨
    }

    // ==================== 러닝 게임 연동 이벤트 ====================
    
    /// <summary>
    /// 러닝 점수 및 보상 계산에 필요한 스냅샷
    /// </summary>
    [Serializable]
    public struct RunningScoreSnapshot
    {
        public Dictionary<EJudgmentType, int> judgmentCounts;   // 판정별 횟수
        public Dictionary<EJudgmentType, int> judgmentScores;   // 판정별 점수 (콤보 포함)
        public float currentHP;                                 // 클리어 시 남은 체력
        public float maxHP;                                     // 최대 체력
        public int baseScore;                                   // 판정 점수 총합
        public int hpScore;                                     // 체력 보너스 점수
        public int comboBonus;                                  // 최대 콤보 보너스 점수
        public int maxComboCount;                               // 최대 콤보 횟수
        public int totalColorChangeCount;                       // 총 색상 전환 횟수
        public int totalScore;                                  // 최종 점수
        public EClearRank clearRank;                            // 최종 등급
    }

    /// <summary>
    /// 러닝 게임 완료 이벤트 
    /// </summary>
    [Serializable]
    public struct RunningGameCompletedEvent
    {
        public int eggId;                 // 사용한 알 ID
        public string eggType;            // 사용한 알 타입 (yellow, blue 등)
        public int stageId;               // 스테이지 ID
        public bool isCleared;            // 클리어 여부
        public RunningScoreSnapshot score;// 점수 스냅샷
        public float distance;            // 달린 거리 (미터) - 필요 시 확장
        public float playTime;            // 플레이 시간 (초)
        public float clearTime;           // 클리어 시간 (클리어한 경우)
        public int[] colorDistances;      // 색상별 달린 거리 [노랑, 파랑, 검정, 빨강, 초록]
    }

    // ==================== UI 관련 이벤트 ====================
    
    /// <summary>
    /// 알 선택 이벤트 (로비에서 육성으로 전환 시)
    /// </summary>
    [Serializable]
    public struct EggSelectedEvent
    {
        public int eggId;           // 알 ID (1~5)
        public string eggType;      // 알 타입 (blue, white, black, red, yellow)
    }
    
    /// <summary>
    /// 육성 화면 열기 이벤트
    /// </summary>
    [Serializable]
    public struct BreedingScreenOpenedEvent
    {
        public int eggId;             // 선택된 알 ID
        public bool isFirstTime;      // 첫 방문 여부
    }

    /// <summary>
    /// 육성 화면 닫기 이벤트
    /// </summary>
    [Serializable]
    public struct BreedingScreenClosedEvent
    {
        public int eggId;             // 닫힌 알 ID
        public float timeSpent;       // 화면에서 보낸 시간
    }

    /// <summary>
    /// 육성 행동 선택 이벤트
    /// </summary>
    [Serializable]
    public struct GrowthActionSelectedEvent
    {
        public int eggId;             // 알 ID
        public int actionId;          // 선택된 행동 ID
        public string actionName;     // 행동 이름
        public bool canAfford;        // 재화 충분 여부
    }

    // ==================== 시스템 관련 이벤트 ====================
    
    /// <summary>
    /// 알 데이터 로드 완료 이벤트
    /// </summary>
    [Serializable]
    public struct EggDataLoadedEvent
    {
        public int eggId;             // 알 ID
        public bool isNewEgg;         // 새로 생성된 알인지 여부
      
        public int[] currentStats;    // 현재 스탯 배열
        public int[] currencyAmounts; // 보유 재화량 배열
    }

    /// <summary>
    /// 데이터 저장 완료 이벤트
    /// </summary>
    [Serializable]
    public struct DataSavedEvent
    {
        public bool success;          // 저장 성공 여부
        public string saveSlot;       // 저장 슬롯
        public float saveTime;        // 저장 시간
        public int eggCount;          // 저장된 알 개수
    }

    /// <summary>
    /// 데이터 로드 완료 이벤트
    /// </summary>
    [Serializable]
    public struct DataLoadedEvent
    {
        public bool success;          // 로드 성공 여부
        public string loadSlot;       // 로드 슬롯
        public float loadTime;        // 로드 시간
        public int eggCount;          // 로드된 알 개수
    }


    // ==================== 도감 관련 이벤트 ====================

    /// <summary>
    /// 도감 진화체 등록 이벤트
    /// </summary>
    [Serializable]
    public struct CollectionFormRegisteredEvent
    {
        public int formId;            // 진화체 ID
        public string formName;       // 진화체 이름 (로컬라이즈 키)
    }

    /// <summary>
    /// 도감 중복 진화체 이벤트
    /// </summary>
    [Serializable]
    public struct CollectionDuplicateFormEvent
    {
        public int formId;            // 진화체 ID
        public int currencyId;        // 지급된 재화 ID
        public int amount;            // 지급된 재화량
    }
}
