using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RollingEgg.Data;
using RollingEgg.Core;

namespace RollingEgg.Core
{
    /// <summary>
    /// 육성 행동 관리 서비스 구현체
    /// 육성 행동 수행, 스탯 관리, 조건 선택 기능을 제공
    /// </summary>
    public class GrowthActionService : IGrowthActionService
    {
        private Dictionary<int, EggData> _eggDataMap = new Dictionary<int, EggData>();
        private Dictionary<int, List<GrowthActionRecord>> _actionHistory = new Dictionary<int, List<GrowthActionRecord>>();
        private GrowthConditionPoolSO _growthConditionPool;
        private EggTableSO _eggTable;
        private StatTypeSO _statType;
        private IEventBus _eventBus;
        private IResourceService _resourceService;
        private ICurrencyService _currencyService;

        private int _currentSessionId;

        public event Action<StatChangedEvent> OnStatChanged;
        public event Action<GrowthActionPerformedEvent> OnActionPerformed;
        public event Action<LevelChangedEvent> OnLevelChanged;

        public async UniTask InitializeAsync()
        {
            Debug.Log("[GrowthActionService] 초기화 시작...");

            // 서비스 의존성 주입
            _eventBus = ServiceLocator.Get<IEventBus>();
            _resourceService = ServiceLocator.Get<IResourceService>();
            _currencyService = ServiceLocator.Get<ICurrencyService>();

            // 세션 ID 초기화
            _currentSessionId = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 테이블 로드
            await LoadGrowthConditionPoolAsync();
            await LoadEggTableAsync();
            await LoadStatTypeAsync();

            // 데이터 로드
            await LoadDataAsync();

            Debug.Log("[GrowthActionService] 초기화 완료");
        }

        private async UniTask LoadGrowthConditionPoolAsync()
        {
            try
            {
                _growthConditionPool = await _resourceService.LoadAssetAsync<GrowthConditionPoolSO>("GrowthConditionPoolSO");
                if (_growthConditionPool == null)
                {
                    Debug.LogError("[GrowthActionService] GrowthConditionPoolSO 로드 실패!");
                    return;
                }

                Debug.Log($"[GrowthActionService] 육성 조건 풀 로드 완료: {_growthConditionPool.Rows.Count}개 조건");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrowthActionService] 육성 조건 풀 로드 중 오류: {e.Message}");
            }
        }

        private async UniTask LoadEggTableAsync()
        {
            try
            {
                _eggTable = await _resourceService.LoadAssetAsync<EggTableSO>("EggTableSO");
                if (_eggTable == null)
                {
                    Debug.LogError("[GrowthActionService] EggTableSO 로드 실패!");
                    return;
                }

                Debug.Log($"[GrowthActionService] 알 테이블 로드 완료: {_eggTable.Rows.Count}개 알");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrowthActionService] 알 테이블 로드 중 오류: {e.Message}");
            }
        }

        private async UniTask LoadStatTypeAsync()
        {
            try
            {
                _statType = await _resourceService.LoadAssetAsync<StatTypeSO>("StatTypeSO");
                if (_statType == null)
                {
                    Debug.LogError("[GrowthActionService] StatTypeSO 로드 실패!");
                    return;
                }

                Debug.Log($"[GrowthActionService] 스탯 타입 테이블 로드 완료: {_statType.Stats.Count}개 스탯");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrowthActionService] 스탯 타입 테이블 로드 중 오류: {e.Message}");
            }
        }

        public int[] GetEggStats(int eggId)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return new int[5]; // 기본 스탯 배열

            return new int[]
            {
                eggData.Courage,
                eggData.Wisdom,
                eggData.Purity,
                eggData.Love,
                eggData.Chaos
            };
        }

        public int GetStatValue(int eggId, StatType statType)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return 0;

            return statType switch
            {
                StatType.Courage => eggData.Courage,
                StatType.Wisdom => eggData.Wisdom,
                StatType.Purity => eggData.Purity,
                StatType.Love => eggData.Love,
                StatType.Chaos => eggData.Chaos,
                _ => 0
            };
        }

        // 일단 경고문 무시. 추후 비동기 작업 추가할 예정 ,예) DB저장, 서버통신 등
        public async UniTask<GrowthActionResult> PerformActionAsync(int eggId, int actionId)
        {
            var result = new GrowthActionResult
            {
                success = false,
                errorMessage = "",
                statChanges = new int[5],
                totalActionCount = 0,
                action = null
            };

            try
            {
                // 알 데이터 확인
                if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                {
                    result.errorMessage = "알 데이터를 찾을 수 없습니다.";
                    return result;
                }

                // 행동 조건 찾기
                var action = FindActionById(actionId);
                if (action == null)
                {
                    result.errorMessage = "육성 행동을 찾을 수 없습니다.";
                    return result;
                }

                result.action = action;

                // 레벨 확인
                if (eggData.level < action.minLevel || eggData.level > action.maxLevel)
                {
                    result.errorMessage = "레벨 조건을 만족하지 않습니다.";
                    return result;
                }

                // 재화 충분 여부 확인
                if (!_currencyService.CanAffordAction(action, eggId, GetEggType(eggId)))
                {
                    result.errorMessage = "재화가 부족합니다.";
                    return result;
                }

                // 재화 소비
                if (!_currencyService.SpendForAction(action, eggId, GetEggType(eggId)))
                {
                    result.errorMessage = "재화 소비에 실패했습니다.";
                    return result;
                }

                // 스탯 변경 적용
                int[] oldStats = GetEggStats(eggId);
                ApplyStatChanges(eggId, action.statIncrements);
                int[] newStats = GetEggStats(eggId);

                // 스탯 변경량 계산
                for (int i = 0; i < 5; i++)
                {
                    result.statChanges[i] = newStats[i] - oldStats[i];
                }

                // 레벨 증가 (육성 행동 1회당 레벨 1씩 증가)
                int oldLevel = eggData.level;
                eggData.level++;
                eggData.totalActionCount = eggData.level; // 호환성을 위해 동기화 (실제로는 level과 동일)
                int newLevel = eggData.level;

                result.totalActionCount = eggData.level; // 레벨을 totalActionCount로 반환 (호환성 유지)

                // 레벨 변경 이벤트 발행
                var levelChangedEvent = new LevelChangedEvent
                {
                    eggId = eggId,
                    oldLevel = oldLevel,
                    newLevel = newLevel,
                    changeAmount = 1,
                    changeReason = "growth_action"
                };
                _eventBus.Publish(levelChangedEvent);
                OnLevelChanged?.Invoke(levelChangedEvent);

                // 행동 기록 추가
                AddActionRecord(eggId, action);

                // 이벤트 발행
                var performedEvent = new GrowthActionPerformedEvent
                {
                    eggId = eggId,
                    actionId = actionId,
                    costCommon = action.costCommon,
                    costSpecial = action.costSpecial,
                    actionName = action.nameLocKey
                };
                _eventBus.Publish(performedEvent);
                OnActionPerformed?.Invoke(performedEvent);

                result.success = true;
                Debug.Log($"[GrowthActionService] 육성 행동 수행 완료: 알ID={eggId}, 행동ID={actionId}, 총횟수={result.totalActionCount}");
            }
            catch (Exception e)
            {
                result.errorMessage = $"육성 행동 수행 중 오류: {e.Message}";
                Debug.LogError($"[GrowthActionService] {result.errorMessage}");
            }

            return result;
        }

        private GrowthConditionPoolSO.ConditionEntry FindActionById(int actionId)
        {
            if (_growthConditionPool == null)
                return null;

            foreach (var action in _growthConditionPool.Rows)
            {
                if (action.id == actionId)
                    return action;
            }

            return null;
        }

        private void ApplyStatChanges(int eggId, List<GrowthConditionPoolSO.StatDelta> statDeltas)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return;

            foreach (var delta in statDeltas)
            {
                int oldValue = GetStatValue(eggId, delta.stat);
                int newValue = Mathf.Max(0, oldValue + delta.amount); // 최소값 0

                // 스탯 설정
                SetStat(eggId, delta.stat, newValue);

                // 이벤트 발행
                var statChangedEvent = new StatChangedEvent
                {
                    eggId = eggId,
                    statType = (int)delta.stat,
                    oldValue = oldValue,
                    newValue = newValue,
                    changeAmount = delta.amount,
                    statName = delta.stat.ToString()
                };
                _eventBus.Publish(statChangedEvent);
                OnStatChanged?.Invoke(statChangedEvent);
            }
        }

        public List<GrowthConditionPoolSO.ConditionEntry> GetAvailableActions(int eggId, int eggLevel, int count = 5)
        {
            if (_growthConditionPool == null)
                return new List<GrowthConditionPoolSO.ConditionEntry>();

            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return new List<GrowthConditionPoolSO.ConditionEntry>();

            // 최근 행동 기록 가져오기 (쿨다운용)
            var recentActions = GetRecentActionIds(eggId);

            // 조건 선택기 사용
            var selectedActions = GrowthConditionSelector.Pick(
                _growthConditionPool, 
                eggLevel, 
                count, 
                recentActions, 
                UnityEngine.Random.Range(0, int.MaxValue)
            );

            return selectedActions ?? new List<GrowthConditionPoolSO.ConditionEntry>();
        }

        public bool CanPerformAction(int eggId, int actionId)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return false;

            var action = FindActionById(actionId);
            if (action == null)
                return false;

            // 레벨 확인
            if (eggData.level < action.minLevel || eggData.level > action.maxLevel)
                return false;

            // 재화 확인
            return _currencyService.CanAffordAction(action, eggId, GetEggType(eggId));
        }

        public int GetTotalActionCount(int eggId)
        {
            // 레벨과 육성행동카운트가 동일 개념이므로 GetEggLevel과 동일하게 동작
            return GetEggLevel(eggId);
        }

        public int GetEggLevel(int eggId)
        {
            return _eggDataMap.TryGetValue(eggId, out var eggData) ? eggData.level : 1;
        }

        public void SetEggLevel(int eggId, int level)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return;

            int oldLevel = eggData.level;
            eggData.level = Mathf.Max(1, level); // 최소 레벨 1

            // 레벨 변경 이벤트 발행
            var levelChangedEvent = new LevelChangedEvent
            {
                eggId = eggId,
                oldLevel = oldLevel,
                newLevel = eggData.level,
                changeAmount = eggData.level - oldLevel,
                changeReason = "manual_set"
            };
            _eventBus.Publish(levelChangedEvent);
            OnLevelChanged?.Invoke(levelChangedEvent);
        }

        public void IncreaseStat(int eggId, StatType statType, int amount, string source = "")
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return;

            int oldValue = GetStatValue(eggId, statType);
            int newValue = Mathf.Max(0, oldValue + amount);

            SetStat(eggId, statType, newValue);

            // 이벤트 발행
            var statChangedEvent = new StatChangedEvent
            {
                eggId = eggId,
                statType = (int)statType,
                oldValue = oldValue,
                newValue = newValue,
                changeAmount = amount,
                statName = statType.ToString()
            };
            _eventBus.Publish(statChangedEvent);
            OnStatChanged?.Invoke(statChangedEvent);
        }

        public void SetStat(int eggId, StatType statType, int value)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return;

            value = Mathf.Max(0, value); // 최소값 0

            switch (statType)
            {
                case StatType.Courage:
                    eggData.Courage = value;
                    break;
                case StatType.Wisdom:
                    eggData.Wisdom = value;
                    break;
                case StatType.Purity:
                    eggData.Purity = value;
                    break;
                case StatType.Love:
                    eggData.Love = value;
                    break;
                case StatType.Chaos:
                    eggData.Chaos = value;
                    break;
            }
        }

        public void ResetEggStats(int eggId)
        {
            if (!_eggDataMap.TryGetValue(eggId, out var eggData))
                return;

            eggData.Courage = 0;
            eggData.Wisdom = 0;
            eggData.Purity = 0;
            eggData.Love = 0;
            eggData.Chaos = 0;
            eggData.level = 1; // 진화 후 레벨을 1로 리셋
            eggData.totalActionCount = 0; // 호환성을 위해 초기화 (실제로는 level과 동일)

            // 레벨 변경 이벤트 발행
            var levelChangedEvent = new LevelChangedEvent
            {
                eggId = eggId,
                oldLevel = eggData.level, // 이전 레벨 (이미 1로 설정됨)
                newLevel = 1,
                changeAmount = 0, // 리셋이므로 변경량 0
                changeReason = "evolution_reset"
            };
            _eventBus.Publish(levelChangedEvent);
            OnLevelChanged?.Invoke(levelChangedEvent);

            // 행동 기록 초기화
            if (_actionHistory.ContainsKey(eggId))
            {
                _actionHistory[eggId].Clear();
            }

            Debug.Log($"[GrowthActionService] 알 스탯 초기화 완료: 알ID={eggId}, 레벨={eggData.level}");
        }

        public void CreateEgg(int eggId)
        {
            // 테이블에서 eggType 조회 (필수)
            if (_eggTable == null || !_eggTable.TryGetById(eggId, out var eggRow))
            {
                Debug.LogError($"[GrowthActionService] 알 ID {eggId}에 해당하는 데이터를 찾을 수 없습니다.");
                return;
            }

            var eggData = new EggData
            {
                eggId = eggId,
                // eggType 제거 - 항상 테이블에서 조회
                level = 1,
                Courage = 0,
                Wisdom = 0,
                Purity = 0,
                Love = 0,
                Chaos = 0,
                totalActionCount = 0, // 호환성을 위해 초기화 (실제로는 level과 동일)
              
            };

            _eggDataMap[eggId] = eggData;
            _actionHistory[eggId] = new List<GrowthActionRecord>();

            Debug.Log($"[GrowthActionService] 새 알 생성: 알ID={eggId}, 타입={eggRow.eggType}");
        }

        public void RemoveEgg(int eggId)
        {
            _eggDataMap.Remove(eggId);
            _actionHistory.Remove(eggId);

            Debug.Log($"[GrowthActionService] 알 삭제: 알ID={eggId}");
        }

        public bool HasEgg(int eggId)
        {
            return _eggDataMap.ContainsKey(eggId);
        }

        public int[] GetAllEggIds()
        {
            var ids = new int[_eggDataMap.Count];
            int index = 0;
            foreach (var kvp in _eggDataMap)
            {
                ids[index++] = kvp.Key;
            }
            return ids;
        }

        public List<GrowthActionRecord> GetActionHistory(int eggId)
        {
            return _actionHistory.TryGetValue(eggId, out var history) ? new List<GrowthActionRecord>(history) : new List<GrowthActionRecord>();
        }

        public string GetEggType(int eggId)
        {
            if (_eggTable != null && _eggTable.TryGetById(eggId, out var row))
                return row.eggType ?? string.Empty;
            return string.Empty;
        }

        public EggTableSO.EggRow GetEggInfo(int eggId)
        {
            if (_eggTable != null && _eggTable.TryGetById(eggId, out var row))
            {
                return row;
            }
            return null;
        }

        public StatTypeSO.StatInfo GetStatInfo(StatType statType)
        {
            if (_statType == null)
                return null;

            foreach (var stat in _statType.Stats)
            {
                if (stat.type == statType)
                    return stat;
            }
            return null;
        }

        public StatTypeSO GetStatTypeSO()
        {
            return _statType;
        }

        public List<int> GetRecentActionIds(int eggId)
        {
            if (!_actionHistory.TryGetValue(eggId, out var history))
                return new List<int>();

            var recentIds = new List<int>();
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            // 최근 10개 행동 기록에서 쿨다운이 적용되는 행동들만 수집
            for (int i = Mathf.Max(0, history.Count - 10); i < history.Count; i++)
            {
                var record = history[i];
                var action = FindActionById(record.actionId);
                
                if (action != null && action.cooldownTurns > 0)
                {
                    // 쿨다운 시간 확인 (예: 1시간 = 3600초)
                    if (currentTime - record.timestamp < action.cooldownTurns * 3600)
                    {
                        recentIds.Add(record.actionId);
                    }
                }
            }

            return recentIds;
        }

        private void AddActionRecord(int eggId, GrowthConditionPoolSO.ConditionEntry action)
        {
            if (!_actionHistory.TryGetValue(eggId, out var history))
            {
                history = new List<GrowthActionRecord>();
                _actionHistory[eggId] = history;
            }

            var record = new GrowthActionRecord
            {
                actionId = action.id,
                actionName = action.nameLocKey,
                statChanges = new int[5], // 실제 변경량은 별도로 계산 필요
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sessionId = _currentSessionId
            };

            history.Add(record);

            // 최대 기록 수 제한 (메모리 관리)
            if (history.Count > 100)
            {
                history.RemoveAt(0);
            }
        }

        public async UniTask SaveDataAsync()
        {
            try
            {
                // Dictionary들을 Serializable 구조체로 변환
                var eggDataEntries = new List<EggDataEntry>();
                foreach (var kvp in _eggDataMap)
                {
                    eggDataEntries.Add(new EggDataEntry
                    {
                        eggId = kvp.Key,
                        eggData = kvp.Value
                    });
                }

                var historyEntries = new List<ActionHistoryEntry>();
                foreach (var kvp in _actionHistory)
                {
                    historyEntries.Add(new ActionHistoryEntry
                    {
                        eggId = kvp.Key,
                        records = kvp.Value.ToArray()
                    });
                }

                var saveData = new GrowthActionSaveData
                {
                    eggDataMap = eggDataEntries.ToArray(),
                    actionHistory = historyEntries.ToArray(),
                    currentSessionId = _currentSessionId,
                    saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string json = JsonUtility.ToJson(saveData, true);
                await System.IO.File.WriteAllTextAsync(GetSaveFilePath(), json);

                Debug.Log("[GrowthActionService] 데이터 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrowthActionService] 데이터 저장 중 오류: {e.Message}");
            }
        }

        public async UniTask LoadDataAsync()
        {
            try
            {
                string filePath = GetSaveFilePath();
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.Log("[GrowthActionService] 저장된 데이터가 없습니다. 초기값으로 시작합니다.");
                    return;
                }

                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var saveData = JsonUtility.FromJson<GrowthActionSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogWarning("[GrowthActionService] 저장된 데이터 형식이 잘못되었습니다. 초기값으로 시작합니다.");
                    return;
                }

                // 구조체 배열들을 Dictionary로 변환
                _eggDataMap = new Dictionary<int, EggData>();
                if (saveData.eggDataMap != null)
                {
                    foreach (var entry in saveData.eggDataMap)
                    {
                        _eggDataMap[entry.eggId] = entry.eggData;
                    }
                }

                _actionHistory = new Dictionary<int, List<GrowthActionRecord>>();
                if (saveData.actionHistory != null)
                {
                    foreach (var entry in saveData.actionHistory)
                    {
                        _actionHistory[entry.eggId] = entry.records != null
                            ? new List<GrowthActionRecord>(entry.records)
                            : new List<GrowthActionRecord>();
                    }
                }

                _currentSessionId = saveData.currentSessionId;

                Debug.Log("[GrowthActionService] 데이터 로드 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GrowthActionService] 데이터 로드 중 오류: {e.Message}");
                _eggDataMap = new Dictionary<int, EggData>();
                _actionHistory = new Dictionary<int, List<GrowthActionRecord>>();
                _currentSessionId = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        private string GetSaveFilePath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "growth_action_data.json");
        }

        [Serializable]
        private class EggData
        {
            public int eggId;
            // eggType 제거 - 항상 EggTableSO에서 조회
            public int level;
            public int Courage;
            public int Wisdom;
            public int Purity;
            public int Love;
            public int Chaos;
            // totalActionCount - 호환성을 위해 유지 (실제로는 level과 동일 개념)
            public int totalActionCount;
            
        }

        [Serializable]
        private class GrowthActionSaveData
        {
            public EggDataEntry[] eggDataMap;
            public ActionHistoryEntry[] actionHistory;
            public int currentSessionId;
            public long saveTime;
        }

        [Serializable]
        private class EggDataEntry
        {
            public int eggId;
            public EggData eggData;
        }

        [Serializable]
        private class ActionHistoryEntry
        {
            public int eggId;
            public GrowthActionRecord[] records;
        }
    }
}



/*
GrowthActionService 구현

▶구현 내용:
ㆍ스탯 관리: 5가지 스탯 (온순함, 활발함, 신중함, 대담함, 예술적) 관리
ㆍ육성 행동 수행: 행동 조건 검증, 재화 소비, 스탯 증가 처리
ㆍ조건 선택: GrowthConditionSelector를 활용한 5개 행동 선택
ㆍ쿨다운 시스템: 행동별 쿨다운 관리로 반복 행동 방지
ㆍ행동 기록: 알별 육성 행동 이력 관리
ㆍ레벨 시스템: 알 레벨에 따른 행동 제한
ㆍ이벤트 시스템: 스탯 변경, 행동 수행 시 EventBus 알림

▶핵심 기능:
ㆍ육성 행동별 공용/전용 재화 소비 검증
ㆍ스탯 변경 시 실시간 이벤트 발행
ㆍ행동 기록을 통한 쿨다운 및 통계 관리
ㆍ알별 독립적인 데이터 관리

*/