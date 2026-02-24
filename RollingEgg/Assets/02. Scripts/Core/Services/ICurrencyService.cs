using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RollingEgg.Data;
using RollingEgg.Util;

namespace RollingEgg.Core
{
    /// <summary>
    /// 재화 관리 서비스 인터페이스
    /// 재화 획득, 소비, 검증, 잔액 조회 기능을 제공
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>
        /// 서비스 초기화
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// 특정 재화의 현재 보유량 조회
        /// </summary>
        /// <param name="currencyId">재화 ID (CurrencyTableSO의 id)</param>
        /// <returns>보유량, 재화가 존재하지 않으면 0</returns>
        int GetCurrencyAmount(int currencyId);

        /// <summary>
        /// 특정 알의 전용 재화 보유량 조회
        /// </summary>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggType">알 타입 (yellow, blue, black, red, white)</param>
        /// <returns>전용 재화 보유량</returns>
        int GetSpecialCurrencyAmount(int eggId, string eggType);

        /// <summary>
        /// 공용 재화 보유량 조회
        /// </summary>
        /// <returns>공용 재화 보유량</returns>
        int GetCommonCurrencyAmount();

        /// <summary>
        /// 재화 획득
        /// </summary>
        /// <param name="currencyId">재화 ID</param>
        /// <param name="amount">획득량</param>
        /// <param name="source">획득 출처</param>
        /// <param name="eggId">관련 알 ID (전용 재화의 경우)</param>
        /// <returns>실제 획득량 (희귀도 배율 적용 후)</returns>
        int AddCurrency(int currencyId, int amount, string source = "", int eggId = -1);

        /// <summary>
        /// 재화 소비 (잔액 검증 포함)
        /// </summary>
        /// <param name="currencyId">재화 ID</param>
        /// <param name="amount">소비량</param>
        /// <param name="purpose">소비 목적</param>
        /// <param name="eggId">관련 알 ID</param>
        /// <returns>소비 성공 여부</returns>
        bool SpendCurrency(int currencyId, int amount, string purpose = "", int eggId = -1);

        /// <summary>
        /// 재화 잔액 충분 여부 확인
        /// </summary>
        /// <param name="currencyId">재화 ID</param>
        /// <param name="amount">필요량</param>
        /// <returns>충분 여부</returns>
        bool HasEnoughCurrency(int currencyId, int amount);

        /// <summary>
        /// 육성 행동에 필요한 재화 충분 여부 확인
        /// </summary>
        /// <param name="action">육성 행동 조건</param>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggType">알 타입</param>
        /// <returns>충분 여부</returns>
        bool CanAffordAction(GrowthConditionPoolSO.ConditionEntry action, int eggId, string eggType);

        /// <summary>
        /// 육성 행동 재화 소비
        /// </summary>
        /// <param name="action">육성 행동 조건</param>
        /// <param name="eggId">알 ID</param>
        /// <param name="eggType">알 타입</param>
        /// <returns>소비 성공 여부</returns>
        bool SpendForAction(GrowthConditionPoolSO.ConditionEntry action, int eggId, string eggType);

        /// <summary>
        /// 러닝 게임 보상 처리
        /// </summary>
        /// <param name="context">보상 계산에 필요한 스냅샷</param>
        /// <returns>획득한 재화 정보</returns>
        UniTask<RewardResult> ProcessRunningRewardAsync(RunningRewardContext context);

        /// <summary>
        /// 모든 재화 정보 조회
        /// </summary>
        /// <returns>재화 ID별 보유량 딕셔너리</returns>
        Dictionary<int, int> GetAllCurrencyAmounts();

        /// <summary>
        /// 재화 테이블 정보 조회
        /// </summary>
        /// <param name="currencyId">재화 ID</param>
        /// <returns>재화 테이블 행 정보</returns>
        CurrencyTableSO.CurrencyRow GetCurrencyInfo(int currencyId);

        /// <summary>
        /// 데이터 저장
        /// </summary>
        UniTask SaveDataAsync();

        /// <summary>
        /// 데이터 로드
        /// </summary>
        UniTask LoadDataAsync();

        /// <summary>
        /// 재화 변경 이벤트 구독
        /// </summary>
        event Action<CurrencyBalanceChangedEvent> OnCurrencyChanged;
    }

    /// <summary>
    /// 러닝 게임 보상 계산을 위한 입력 데이터
    /// </summary>
    public struct RunningRewardContext
    {
        public int eggId;
        public string eggType;
        public bool isCleared;
        public int totalScore;
        public EClearRank rank;
    }

    /// <summary>
    /// 러닝 게임 보상 결과
    /// </summary>
    public struct RewardResult
    {
        public int commonCurrency;        // 획득한 공용 재화
        public int specialCurrency;       // 획득한 전용 재화
        public string[] rewardSources;    // 보상 출처 배열
    }
}
