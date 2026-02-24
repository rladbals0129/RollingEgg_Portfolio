using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RollingEgg.Data;
using RollingEgg.Core;
using RollingEgg.Util;

namespace RollingEgg.Core
{
    /// <summary>
    /// 재화 관리 서비스 구현체
    /// 재화 획득, 소비, 검증, 잔액 조회 기능을 제공
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private Dictionary<int, int> _currencyAmounts = new Dictionary<int, int>();
        private CurrencyTableSO _currencyTable;
        private IEventBus _eventBus;
        private IResourceService _resourceService;
        private ICollectionService _collectionService;

        // 재화 ID 상수
        private const int COMMON_CURRENCY_ID = 1; // 공용 재화 ID (CurrencyTableSO 기준)
     

        public event Action<CurrencyBalanceChangedEvent> OnCurrencyChanged;

        public async UniTask InitializeAsync()
        {
            Debug.Log("[CurrencyService] 초기화 시작...");

            // 서비스 의존성 주입
            _eventBus = ServiceLocator.Get<IEventBus>();
            _resourceService = ServiceLocator.Get<IResourceService>();

            // 재화 테이블 로드
            await LoadCurrencyTableAsync();

            // 데이터 로드
            await LoadDataAsync();

            Debug.Log("[CurrencyService] 초기화 완료");
        }

        private async UniTask LoadCurrencyTableAsync()
        {
            try
            {
                _currencyTable = await _resourceService.LoadAssetAsync<CurrencyTableSO>("CurrencyTableSO");
                if (_currencyTable == null)
                {
                    Debug.LogError("[CurrencyService] CurrencyTableSO 로드 실패!");
                    return;
                }

                // 재화별 초기화
                foreach (var row in _currencyTable.Rows)
                {
                    if (!_currencyAmounts.ContainsKey(row.id))
                    {
                        _currencyAmounts[row.id] = 0;
                    }
                }

                Debug.Log($"[CurrencyService] 재화 테이블 로드 완료: {_currencyTable.Rows.Count}개 재화");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyService] 재화 테이블 로드 중 오류: {e.Message}");
            }
        }

        public int GetCurrencyAmount(int currencyId)
        {
            return _currencyAmounts.TryGetValue(currencyId, out int amount) ? amount : 0;
        }

        public int GetSpecialCurrencyAmount(int eggId, string eggType)
        {
            // 전용 재화 ID 찾기 (알 타입에 따라)
            int specialCurrencyId = GetSpecialCurrencyId(eggType);
            return GetCurrencyAmount(specialCurrencyId);
        }

        public int GetCommonCurrencyAmount()
        {
            return GetCurrencyAmount(COMMON_CURRENCY_ID);
        }

        private int GetSpecialCurrencyId(string eggType)
        {
            // 알 타입별 전용 재화 ID 매핑
            return eggType.ToLower() switch
            {
                "blue" => 2,
                "red" => 3,
                "white" => 4,
                "black" => 5,
                "yellow" => 6,
                _ => -1
            };
        }

        public int AddCurrency(int currencyId, int amount, string source = "", int eggId = -1)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyService] 잘못된 재화 획득량: {amount}");
                return 0;
            }

            // 재화 테이블에서 희귀도 배율 적용
            var currencyInfo = GetCurrencyInfo(currencyId);
            if (currencyInfo == null)
            {
                Debug.LogError($"[CurrencyService] 알 수 없는 재화 ID: {currencyId}");
                return 0;
            }

            int actualAmount = Mathf.FloorToInt(amount * currencyInfo.rarity);
            int oldAmount = GetCurrencyAmount(currencyId);
            int newAmount = oldAmount + actualAmount;

            _currencyAmounts[currencyId] = newAmount;

            // 이벤트 발행
            var gainedEvent = new CurrencyGainedEvent
            {
                currencyId = currencyId,
                amount = actualAmount,
                source = source,
                eggId = eggId
            };
            _eventBus.Publish(gainedEvent);

            var changedEvent = new CurrencyBalanceChangedEvent
            {
                currencyId = currencyId,
                oldAmount = oldAmount,
                newAmount = newAmount,
                changeAmount = actualAmount
            };
            _eventBus.Publish(changedEvent);
            OnCurrencyChanged?.Invoke(changedEvent);

            Debug.Log($"[CurrencyService] 재화 획득: ID={currencyId}, 기본량={amount}, 실제량={actualAmount}, 총량={newAmount}, 출처={source}");
            return actualAmount;
        }

        public bool SpendCurrency(int currencyId, int amount, string purpose = "", int eggId = -1)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[CurrencyService] 잘못된 재화 소비량: {amount}");
                return false;
            }

            if (!HasEnoughCurrency(currencyId, amount))
            {
                Debug.LogWarning($"[CurrencyService] 재화 부족: ID={currencyId}, 필요량={amount}, 보유량={GetCurrencyAmount(currencyId)}");
                return false;
            }

            int oldAmount = GetCurrencyAmount(currencyId);
            int newAmount = oldAmount - amount;

            _currencyAmounts[currencyId] = newAmount;

            // 이벤트 발행
            var spentEvent = new CurrencySpentEvent
            {
                currencyId = currencyId,
                amount = amount,
                purpose = purpose,
                eggId = eggId
            };
            _eventBus.Publish(spentEvent);

            var changedEvent = new CurrencyBalanceChangedEvent
            {
                currencyId = currencyId,
                oldAmount = oldAmount,
                newAmount = newAmount,
                changeAmount = -amount
            };
            _eventBus.Publish(changedEvent);
            OnCurrencyChanged?.Invoke(changedEvent);

            Debug.Log($"[CurrencyService] 재화 소비: ID={currencyId}, 소비량={amount}, 남은량={newAmount}, 목적={purpose}");
            return true;
        }

        public bool HasEnoughCurrency(int currencyId, int amount)
        {
            return GetCurrencyAmount(currencyId) >= amount;
        }

        public bool CanAffordAction(GrowthConditionPoolSO.ConditionEntry action, int eggId, string eggType)
        {
            // 공용 재화 확인
            if (action.costCommon > 0 && !HasEnoughCurrency(COMMON_CURRENCY_ID, action.costCommon))
                return false;

            // 전용 재화 확인
            if (action.costSpecial > 0)
            {
                int specialCurrencyId = GetSpecialCurrencyId(eggType);
                if (specialCurrencyId == -1 || !HasEnoughCurrency(specialCurrencyId, action.costSpecial))
                    return false;
            }

            return true;
        }

        public bool SpendForAction(GrowthConditionPoolSO.ConditionEntry action, int eggId, string eggType)
        {
            // 공용 재화 소비
            if (action.costCommon > 0)
            {
                if (!SpendCurrency(COMMON_CURRENCY_ID, action.costCommon, "growth_action", eggId))
                    return false;
            }

            // 전용 재화 소비
            if (action.costSpecial > 0)
            {
                int specialCurrencyId = GetSpecialCurrencyId(eggType);
                if (specialCurrencyId == -1 || !SpendCurrency(specialCurrencyId, action.costSpecial, "growth_action", eggId))
                    return false;
            }

            return true;
        }

        // 일단 경고문 무시. 추후 비동기 작업 추가할 예정 ,예) DB저장, 서버통신 등
        public async UniTask<RewardResult> ProcessRunningRewardAsync(RunningRewardContext context)
        {
            var result = new RewardResult
            {
                commonCurrency = 0,
                specialCurrency = 0,
                rewardSources = Array.Empty<string>()
            };

            if (!context.isCleared || context.totalScore <= 0)
            {
                Debug.Log("[CurrencyService] 러닝 보상 처리 스킵 - 미클리어 혹은 점수 0");
                return result;
            }

            try
            {
                await UniTask.Yield();

                string normalizedEggType = string.IsNullOrEmpty(context.eggType)
                    ? "blue"
                    : context.eggType.ToLowerInvariant();

                int baseCommonCurrency = Mathf.Max(0, context.totalScore);
                int baseSpecialCurrency = Mathf.Max(0, ScoreUtil.GetRewardByRank(context.rank));

                var collectionService = ResolveCollectionService();
                float commonBuffPercent = collectionService?.GetCommonCurrencyBuffPercent() ?? 0f;
                float specialBuffPercent = collectionService?.GetSpecialCurrencyBuffPercent(normalizedEggType) ?? 0f;

                int buffedCommonCurrency = ApplyPercentBuff(baseCommonCurrency, commonBuffPercent);
                int buffedSpecialCurrency = ApplyPercentBuff(baseSpecialCurrency, specialBuffPercent);

                int extraCommonCurrency = buffedCommonCurrency - baseCommonCurrency;
                int extraSpecialCurrency = buffedSpecialCurrency - baseSpecialCurrency;
                if (extraCommonCurrency != 0 || extraSpecialCurrency != 0)
                {
                    Debug.Log($"[CurrencyService] Collection buff applied: eggType={normalizedEggType}, " +
                        $"common+={extraCommonCurrency} ({commonBuffPercent}%), special+={extraSpecialCurrency} ({specialBuffPercent}%)");
                }

                if (buffedCommonCurrency > 0)
                {
                    result.commonCurrency = AddCurrency(COMMON_CURRENCY_ID, buffedCommonCurrency, "running_game", context.eggId);
                }

                if (buffedSpecialCurrency > 0)
                {
                    int specialCurrencyId = GetSpecialCurrencyId(normalizedEggType);
                    if (specialCurrencyId != -1)
                    {
                        result.specialCurrency = AddCurrency(specialCurrencyId, buffedSpecialCurrency, "running_game", context.eggId);
                    }
                }

                var sources = new List<string>();
                if (result.commonCurrency > 0) sources.Add("common_currency");
                if (result.specialCurrency > 0) sources.Add("special_currency");
                result.rewardSources = sources.ToArray();

                Debug.Log($"[CurrencyService] 러닝 보상 처리 완료: 점수={context.totalScore}, 등급={context.rank}, 공용={result.commonCurrency}, 전용={result.specialCurrency}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyService] 러닝 게임 보상 처리 중 오류: {e.Message}");
            }

            return result;
        }

        private ICollectionService ResolveCollectionService()
        {
            if (_collectionService == null && ServiceLocator.HasService<ICollectionService>())
            {
                _collectionService = ServiceLocator.Get<ICollectionService>();
            }

            return _collectionService;
        }

        private static int ApplyPercentBuff(int baseValue, float percent)
        {
            if (baseValue <= 0 || Math.Abs(percent) <= float.Epsilon)
                return baseValue;

            float multiplier = 1f + percent * 0.01f;
            return Mathf.FloorToInt(baseValue * multiplier);
        }


        public Dictionary<int, int> GetAllCurrencyAmounts()
        {
            return new Dictionary<int, int>(_currencyAmounts);
        }

        public CurrencyTableSO.CurrencyRow GetCurrencyInfo(int currencyId)
        {
            if (_currencyTable == null)
                return null;

            return _currencyTable.TryGetById(currencyId, out var row) ? row : null;
        }

        

        public async UniTask SaveDataAsync()
        {
            try
            {
                // Dictionary를 CurrencyEntry[]로 변환
                var currencyEntries = new List<CurrencyEntry>();
                foreach (var kvp in _currencyAmounts)
                {
                    currencyEntries.Add(new CurrencyEntry
                    {
                        id = kvp.Key,
                        amount = kvp.Value
                    });
                }

                var saveData = new CurrencySaveData
                {
                    currencyAmounts = currencyEntries.ToArray(),
                    saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string json = JsonUtility.ToJson(saveData, true);
                await System.IO.File.WriteAllTextAsync(GetSaveFilePath(), json);

                Debug.Log("[CurrencyService] 데이터 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyService] 데이터 저장 중 오류: {e.Message}");
            }
        }

        public async UniTask LoadDataAsync()
        {
            try
            {
                string filePath = GetSaveFilePath();
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.Log("[CurrencyService] 저장된 데이터가 없습니다. 초기값으로 시작합니다.");
                    return;
                }

                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var saveData = JsonUtility.FromJson<CurrencySaveData>(json);

                // CurrencyEntry[]를 Dictionary로 변환
                _currencyAmounts = new Dictionary<int, int>();
                if (saveData.currencyAmounts != null)
                {
                    foreach (var entry in saveData.currencyAmounts)
                    {
                        _currencyAmounts[entry.id] = entry.amount;
                    }
                }

                Debug.Log("[CurrencyService] 데이터 로드 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyService] 데이터 로드 중 오류: {e.Message}");
                _currencyAmounts = new Dictionary<int, int>();
            }
        }

        private string GetSaveFilePath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "currency_data.json");
        }

        [Serializable]
        private class CurrencySaveData
        {
            public CurrencyEntry[] currencyAmounts;
            public long saveTime;
        }

        [Serializable]
        private class CurrencyEntry
        {
            public int id;
            public int amount;
        }
    }
}


/*
CurrencyService 구현

▶구현 내용:
ㆍ재화 관리: 획득/소비/검증/잔액 조회 기능
ㆍ러닝 게임 보상 처리: 거리 기반 공식에 따른 재화 계산
ㆍ희귀도 배율 적용: 재화 테이블의 희귀도에 따른 획득량 조정
ㆍ이벤트 시스템: 재화 변경 시 EventBus를 통한 알림
ㆍ데이터 저장/로드: JSON 기반 영구 저장
ㆍ육성 행동 지원: 행동별 재화 소비 검증 및 처리

▶핵심 기능:
ㆍ거리 1m당 공용재화 10개, 전용재화 15개 기본 획득
ㆍ클리어 시 1.5배 보너스, 클리어 타임에 따른 추가 보너스
ㆍ알 타입별 희귀도 배율 (노랑 1.0배 → 하양 1.4배)
ㆍ육성 행동별 재화 소비 검증 및 처리

*/
