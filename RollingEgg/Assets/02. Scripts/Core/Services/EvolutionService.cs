using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RollingEgg.Data;
using RollingEgg.Core;

namespace RollingEgg.Core
{
    /// <summary>
    /// 진화 관리 서비스 구현체
    /// 진화 조건 판정, 진화 처리, 결과 결정 기능을 제공
    /// </summary>
    public class EvolutionService : IEvolutionService
    {
        private Dictionary<int, EvolutionRequirement> _evolutionRequirements = new Dictionary<int, EvolutionRequirement>();
        private Dictionary<int, List<EvolutionRecord>> _evolutionHistory = new Dictionary<int, List<EvolutionRecord>>();
        private Dictionary<int, EvolutionForm> _evolutionForms = new Dictionary<int, EvolutionForm>();
        private Dictionary<int, int> _currentEvolutionStages = new Dictionary<int, int>();

        private IEventBus _eventBus;
        private IResourceService _resourceService;
        private IGrowthActionService _growthActionService;

        // 테이블 데이터 (Addressables)
        private EvolutionRuleTableSO _evolutionRuleTable;
        private EvolvedFormTableSO _evolvedFormTable;

        public event Action<EvolutionAttemptEvent> OnEvolutionAttempted;
        public event Action<EvolutionCompletedEvent> OnEvolutionCompleted;
        public event Action<EvolutionFailedEvent> OnEvolutionFailed;

        public async UniTask InitializeAsync()
        {
            Debug.Log("[EvolutionService] 초기화 시작...");

            // 서비스 의존성 주입
            _eventBus = ServiceLocator.Get<IEventBus>();
            _resourceService = ServiceLocator.Get<IResourceService>();
            _growthActionService = ServiceLocator.Get<IGrowthActionService>();

            // 테이블 로드 및 캐시 구성
            await LoadTablesAsync();

            // 데이터 로드
            await LoadDataAsync();

            Debug.Log("[EvolutionService] 초기화 완료");
        }

        private async UniTask LoadTablesAsync()
        {
            try
            {
                _evolutionRuleTable = await _resourceService.LoadAssetAsync<EvolutionRuleTableSO>("EvolutionRuleTableSO");
                if (_evolutionRuleTable == null)
                    Debug.LogWarning("[EvolutionService] EvolutionRuleTableSO 로드 실패 또는 미등록");

                _evolvedFormTable = await _resourceService.LoadAssetAsync<EvolvedFormTableSO>("EvolvedFormTableSO");
                if (_evolvedFormTable == null)
                {
                    Debug.LogWarning("[EvolutionService] EvolvedFormTableSO 로드 실패 또는 미등록");
                    return;
                }

                // 폼 캐시 구성(최소 정보)
                _evolutionForms.Clear();
                foreach (var row in _evolvedFormTable.Rows)
                {
                    if (row == null) continue;
                    var form = new EvolutionForm
                    {
                        formId = row.id,
                        formName = row.nameLocKey,
                        eggType = row.eggType,
                        primaryStat = StatType.Courage, // 기본값(룰에 따라 Determine 시 설정)
                        requiredStatValue = 0,
                        probability = 0f,
                        description = string.Empty,
                        iconAddress = row.icon != null ? row.icon.AssetGUID : string.Empty,
                    unlockConditions = Array.Empty<string>(),
                    buffType = row.buffType,
                    buffValuePercent = row.buffValuePercent,
                    buffTargetEggType = row.buffTargetEggType
                    };
                    _evolutionForms[row.id] = form;
                }

                Debug.Log($"[EvolutionService] 테이블 로드 완료: Rules={_evolutionRuleTable?.Rows.Count ?? 0}, Forms={_evolvedFormTable.Rows.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvolutionService] 테이블 로드 중 오류: {e.Message}");
            }
        }

        public EvolutionCondition CheckEvolutionCondition(int eggId, int nurtureLevel)
        {
            var condition = new EvolutionCondition
            {
                canEvolve = false,
                hasEnoughLevel = false,
                requiredLevel = 0,
                currentLevel = nurtureLevel,
                errorMessage = ""
            };

            try
            {
                // 진화 요구사항 가져오기 (기본값 설정)
                if (!_evolutionRequirements.TryGetValue(eggId, out var requirement))
                {
                    requirement = GetDefaultEvolutionRequirement(eggId);
                    _evolutionRequirements[eggId] = requirement;
                }

                condition.requiredLevel = requirement.requiredLevel;

                // 육성 레벨 확인
                condition.hasEnoughLevel = nurtureLevel >= condition.requiredLevel;

                // 진화 가능 여부 판정
                condition.canEvolve = condition.hasEnoughLevel;

                if (!condition.canEvolve)
                {
                    condition.errorMessage = "육성 레벨이 부족합니다.";
                }

                Debug.Log($"[EvolutionService] 진화 조건 확인: 알ID={eggId}, 가능={condition.canEvolve}, 레벨={nurtureLevel}/{condition.requiredLevel}");
            }
            catch (Exception e)
            {
                condition.errorMessage = $"진화 조건 확인 중 오류: {e.Message}";
                Debug.LogError($"[EvolutionService] {condition.errorMessage}");
            }

            return condition;
        }

        private EvolutionRequirement GetDefaultEvolutionRequirement(int eggId)
        {
            // 알 ID나 타입에 따라 다른 요구사항 설정 가능
            return new EvolutionRequirement
            {
                requiredLevel = 5        // 기본 5레벨 육성
            };
        }

        // 일단 경고문 무시. 추후 비동기 작업 추가할 예정 ,예) DB저장, 서버통신 등
        public async UniTask<EvolutionResult> AttemptEvolutionAsync(int eggId, int nurtureLevel, int[] currentStats)
        {
            var result = new EvolutionResult
            {
                success = false,
                evolutionFormId = 0,
                evolutionName = "",
                finalStats = new int[0],
                evolutionTime = 0f,
                errorMessage = "",
                formInfo = new EvolutionForm()
            };

            await UniTask.Yield();

            try
            {
                var startTime = Time.time;

                // 진화 조건 확인
                var condition = CheckEvolutionCondition(eggId, nurtureLevel);

                // 이벤트 발행 (진화 시도)
                var attemptEvent = new EvolutionAttemptEvent
                {
                    eggId = eggId,
                    nurtureLevel = nurtureLevel,
                    requiredLevel = condition.requiredLevel,
                    canEvolve = condition.canEvolve
                };
                _eventBus.Publish(attemptEvent);
                OnEvolutionAttempted?.Invoke(attemptEvent);

                if (!condition.canEvolve)
                {
                    result.errorMessage = condition.errorMessage;
                    
                    // 진화 실패 이벤트
                    var failedEvent = new EvolutionFailedEvent
                    {
                        eggId = eggId,
                        reason = condition.errorMessage,
                        missingLevel = condition.hasEnoughLevel ? 0 : condition.requiredLevel - nurtureLevel
                    };
                    _eventBus.Publish(failedEvent);
                    OnEvolutionFailed?.Invoke(failedEvent);

                    return result;
                }

                // 알 타입 가져오기 (GrowthActionService에서)
                var eggIds = _growthActionService.GetAllEggIds();
                if (!eggIds.Contains(eggId))
                {
                    result.errorMessage = "알 데이터를 찾을 수 없습니다.";
                    return result;
                }

                // 진화 형태 결정
                string eggType = GetEggType(eggId);
                var evolutionForm = DetermineEvolutionForm(eggId, eggType, nurtureLevel, currentStats);

                if (evolutionForm.formId == 0)
                {
                    result.errorMessage = "진화할 수 있는 형태가 없습니다.";
                    return result;
                }

                // 진화 처리
                result.evolutionFormId = evolutionForm.formId;
                result.evolutionName = evolutionForm.formName;
                result.finalStats = new int[currentStats.Length];
                Array.Copy(currentStats, result.finalStats, currentStats.Length);
                result.formInfo = evolutionForm;
                result.evolutionTime = Time.time - startTime;

                // 진화 기록 추가
                AddEvolutionRecord(eggId, result);

                // 진화 단계 업데이트
                _currentEvolutionStages[eggId] = (_currentEvolutionStages.TryGetValue(eggId, out int currentStage) ? currentStage : 0) + 1;

                // 진화 완료 이벤트
                var completedEvent = new EvolutionCompletedEvent
                {
                    eggId = eggId,
                    evolutionFormId = result.evolutionFormId,
                    evolutionName = result.evolutionName,
                    finalStats = result.finalStats,
                    evolutionTime = result.evolutionTime
                };
                _eventBus.Publish(completedEvent);
                OnEvolutionCompleted?.Invoke(completedEvent);

                // 도감 등록 처리
                var collectionService = ServiceLocator.Get<ICollectionService>();
                bool isNewForm = collectionService.RegisterEvolutionForm(result.evolutionFormId);
                result.isNewForm = isNewForm;
                result.duplicateReward = isNewForm ? 0 : collectionService.ProcessDuplicateForm(result.evolutionFormId);

                result.success = true;
                Debug.Log($"[EvolutionService] 진화 완료: 알ID={eggId}, 형태ID={result.evolutionFormId}, 시간={result.evolutionTime:F2}초");
            }
            catch (Exception e)
            {
                result.errorMessage = $"진화 처리 중 오류: {e.Message}";
                Debug.LogError($"[EvolutionService] {result.errorMessage}");
            }

            return result;
        }

        // GrowthActionService로부터 알 타입을 조회해 사용하도록 변경
        private string GetEggType(int eggId)
        {
            try
            {
                var type = _growthActionService.GetEggType(eggId);
                if (!string.IsNullOrEmpty(type)) return type;
            }
            catch { }
            return "yellow"; // 안전장치: 타입 미등록 시 기본값
        }

        public EvolutionForm DetermineEvolutionForm(int eggId, string eggType, int nurtureLevel, int[] finalStats)
        {
            // 스탯 맵 구성 및 지배 스탯 산출
            var statDict = new Dictionary<StatType, int>();
            for (int i = 0; i < finalStats.Length && i < 5; i++)
                statDict[(StatType)i] = finalStats[i];

            var dominant = StatType.Courage;
            int maxVal = int.MinValue;
            foreach (var kv in statDict)
            {
                if (kv.Value > maxVal)
                {
                    maxVal = kv.Value;
                    dominant = kv.Key;
                }
            }

            // 육성 레벨 기반으로 룰 행 선택 (eggId==0 또는 알 미보유 시 시뮬레이션용 최대값)
            int level = nurtureLevel;
            if (eggId == 0 || !_growthActionService.HasEgg(eggId))
                level = int.MaxValue;

            if (_evolutionRuleTable == null || _evolutionRuleTable.Rows.Count == 0)
                return new EvolutionForm();

            var candidate = _evolutionRuleTable.Rows
                .Where(r => r != null
                            && string.Equals(r.eggType, eggType, StringComparison.OrdinalIgnoreCase)
                            && r.dominantStat == dominant
                            && r.minNurtureLevel <= level)
                .OrderByDescending(r => r.minNurtureLevel)
                .FirstOrDefault();

            if (candidate == null || candidate.outcomes == null || candidate.outcomes.Count == 0)
                return new EvolutionForm();

            // 확률 선택
            float sum = 0f;
            foreach (var o in candidate.outcomes)
                sum += Mathf.Max(0f, o.probabilityPercent);
            if (sum <= 0f) return new EvolutionForm();

            float roll = UnityEngine.Random.Range(0f, sum);
            float acc = 0f;
            int chosenFormId = 0;
            foreach (var o in candidate.outcomes)
            {
                float p = Mathf.Max(0f, o.probabilityPercent);
                acc += p;
                if (roll <= acc)
                {
                    chosenFormId = o.formId;
                    break;
                }
            }

            if (chosenFormId == 0)
                return new EvolutionForm();

            // 폼 정보 조립
            if (_evolutionForms.TryGetValue(chosenFormId, out var formInfo))
            {
                formInfo.primaryStat = dominant;
                return formInfo;
            }
            else
            {
                // 최소 정보라도 구성
                return new EvolutionForm
                {
                    formId = chosenFormId,
                    formName = $"form_{chosenFormId}",
                    eggType = eggType,
                    primaryStat = dominant,
                    requiredStatValue = 0,
                    probability = 0f,
                    description = string.Empty,
                    iconAddress = string.Empty,
                    unlockConditions = Array.Empty<string>(),
                    buffType = EvolvedFormTableSO.BuffType.None,
                    buffValuePercent = 0f,
                    buffTargetEggType = string.Empty
                };
            }
        }

        public float CalculateEvolutionProbability(StatType statType, int statValue, 
            Dictionary<StatType, int> otherStats)
        {
            // 기본 확률 계산 (주요 스탯 값 기반)
            float baseProbability = Mathf.Clamp01(statValue / 100f);

            // 다른 스탯들의 균형 보너스
            float balanceBonus = 0f;
            int totalOtherStats = 0;
            int maxOtherStat = 0;

            foreach (var kvp in otherStats)
            {
                if (kvp.Key != statType)
                {
                    totalOtherStats += kvp.Value;
                    maxOtherStat = Mathf.Max(maxOtherStat, kvp.Value);
                }
            }

            // 균형 잡힌 스탯에 보너스 (너무 한쪽으로 치우치지 않았을 때)
            float averageOtherStats = totalOtherStats / (float)Mathf.Max(1, otherStats.Count - 1);
            if (Mathf.Abs(maxOtherStat - averageOtherStats) < 20f)
            {
                balanceBonus = 0.1f; // 10% 보너스
            }

            return Mathf.Clamp01(baseProbability + balanceBonus);
        }

        public int GetCurrentEvolutionStage(int eggId)
        {
            return _currentEvolutionStages.TryGetValue(eggId, out int stage) ? stage : 0;
        }

        public List<EvolutionRecord> GetEvolutionHistory(int eggId)
        {
            return _evolutionHistory.TryGetValue(eggId, out var history) ? new List<EvolutionRecord>(history) : new List<EvolutionRecord>();
        }

        public EvolutionForm GetEvolutionFormInfo(int formId)
        {
            return _evolutionForms.TryGetValue(formId, out var form) ? form : new EvolutionForm();
        }

        public List<EvolutionForm> GetAvailableEvolutionForms(string eggType)
        {
            return _evolutionForms.Values
                .Where(form => form.eggType.Equals(eggType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void SetEvolutionRequirement(int eggId, int requiredLevel)
        {
            _evolutionRequirements[eggId] = new EvolutionRequirement
            {
                requiredLevel = requiredLevel
            };

            Debug.Log($"[EvolutionService] 진화 요구사항 설정: 알ID={eggId}, 레벨={requiredLevel}");
        }

        public void ResetEggAfterEvolution(int eggId, int newFormId)
        {
            // GrowthActionService를 통해 알 스탯 초기화
            _growthActionService.ResetEggStats(eggId);

            Debug.Log($"[EvolutionService] 진화 후 알 초기화: 알ID={eggId}, 새형태={newFormId}");
        }

        public EvolutionSimulationResult SimulateEvolution(string eggType, int[] stats, int simulationCount = 1000)
        {
            var result = new EvolutionSimulationResult
            {
                totalSimulations = simulationCount,
                formResults = new Dictionary<int, int>(),
                statProbabilities = new Dictionary<StatType, float>(),
                averageEvolutionTime = 0f,
                mostCommonForm = ""
            };

            var availableForms = GetAvailableEvolutionForms(eggType);
            var statDict = new Dictionary<StatType, int>();
            
            for (int i = 0; i < stats.Length && i < 5; i++)
            {
                statDict[(StatType)i] = stats[i];
            }

            // 시뮬레이션 실행
            for (int i = 0; i < simulationCount; i++)
            {
                var form = DetermineEvolutionForm(0, eggType, int.MaxValue, stats);
                if (form.formId != 0)
                {
                    if (!result.formResults.ContainsKey(form.formId))
                        result.formResults[form.formId] = 0;
                    result.formResults[form.formId]++;
                }
            }

            // 결과 분석
            if (result.formResults.Count > 0)
            {
                var mostCommon = result.formResults.OrderByDescending(x => x.Value).First();
                result.mostCommonForm = GetEvolutionFormInfo(mostCommon.Key).formName;
            }

            // 스탯별 확률 계산
            foreach (var statType in statDict.Keys)
            {
                result.statProbabilities[statType] = CalculateEvolutionProbability(statType, statDict[statType], statDict);
            }

            Debug.Log($"[EvolutionService] 진화 시뮬레이션 완료: {simulationCount}회, 결과={result.formResults.Count}개 형태");
            return result;
        }

        public List<PredictedEvolutionOutcome> GetPredictedEvolutionOutcomes(int eggId, string eggType, int nurtureLevel, int[] currentStats)
        {
            var outcomes = new List<PredictedEvolutionOutcome>();

            try
            {
                if (_evolutionRuleTable == null || _evolutionRuleTable.Rows.Count == 0)
                {
                    Debug.LogWarning("[EvolutionService] EvolutionRuleTable이 없거나 비어있습니다.");
                    return outcomes;
                }

                // 지배 스탯 산출
                var statDict = new Dictionary<StatType, int>();
                for (int i = 0; i < currentStats.Length && i < 5; i++)
                    statDict[(StatType)i] = currentStats[i];

                var dominant = StatType.Courage;
                int maxVal = int.MinValue;
                foreach (var kv in statDict)
                {
                    if (kv.Value > maxVal)
                    {
                        maxVal = kv.Value;
                        dominant = kv.Key;
                    }
                }

                // 육성 레벨 기반으로 룰 행 선택
                int level = nurtureLevel;
                if (eggId == 0 || !_growthActionService.HasEgg(eggId))
                    level = int.MaxValue;

                var candidate = _evolutionRuleTable.Rows
                    .Where(r => r != null
                                && string.Equals(r.eggType, eggType, StringComparison.OrdinalIgnoreCase)
                                && r.dominantStat == dominant
                                && r.minNurtureLevel <= level)
                    .OrderByDescending(r => r.minNurtureLevel)
                    .FirstOrDefault();

                if (candidate == null || candidate.outcomes == null || candidate.outcomes.Count == 0)
                {
                    Debug.LogWarning($"[EvolutionService] 해당 조건에 맞는 진화 룰을 찾을 수 없습니다. (알타입={eggType}, 지배스탯={dominant}, 레벨={nurtureLevel})");
                    return outcomes;
                }

                // 가능한 진화 결과 3종 추출
                foreach (var outcome in candidate.outcomes)
                {
                    if (outcome.probabilityPercent <= 0f) continue;

                    var formInfo = GetEvolutionFormInfo(outcome.formId);
                    if (formInfo.formId == 0 && !_evolutionForms.ContainsKey(outcome.formId))
                    {
                        // 폼 정보가 없으면 기본 정보로 생성
                        outcomes.Add(new PredictedEvolutionOutcome
                        {
                            formId = outcome.formId,
                            formName = $"form_{outcome.formId}",
                            probabilityPercent = outcome.probabilityPercent,
                            iconAddress = string.Empty
                        });
                    }
                    else
                    {
                        outcomes.Add(new PredictedEvolutionOutcome
                        {
                            formId = outcome.formId,
                            formName = formInfo.formName,
                            probabilityPercent = outcome.probabilityPercent,
                            iconAddress = formInfo.iconAddress
                        });
                    }
                }

                Debug.Log($"[EvolutionService] 예상 진화 결과 조회: {outcomes.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvolutionService] 예상 진화 결과 조회 중 오류: {e.Message}");
            }

            return outcomes;
        }

        private void AddEvolutionRecord(int eggId, EvolutionResult result)
        {
            if (!_evolutionHistory.TryGetValue(eggId, out var history))
            {
                history = new List<EvolutionRecord>();
                _evolutionHistory[eggId] = history;
            }

            var record = new EvolutionRecord
            {
                evolutionFormId = result.evolutionFormId,
                evolutionName = result.evolutionName,
                statsAtEvolution = new int[result.finalStats.Length],
                evolutionTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sessionId = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                evolutionDuration = result.evolutionTime
            };

            Array.Copy(result.finalStats, record.statsAtEvolution, result.finalStats.Length);
            history.Add(record);

            // 최대 기록 수 제한
            if (history.Count > 50)
            {
                history.RemoveAt(0);
            }
        }

        public async UniTask SaveDataAsync()
        {
            try
            {
                // Dictionary들을 Serializable 구조체로 변환
                var requirementEntries = new List<EvolutionRequirementEntry>();
                foreach (var kvp in _evolutionRequirements)
                {
                    requirementEntries.Add(new EvolutionRequirementEntry
                    {
                        eggId = kvp.Key,
                        requirement = kvp.Value
                    });
                }

                var historyEntries = new List<EvolutionHistoryEntry>();
                foreach (var kvp in _evolutionHistory)
                {
                    historyEntries.Add(new EvolutionHistoryEntry
                    {
                        eggId = kvp.Key,
                        records = kvp.Value.ToArray()
                    });
                }

                var stageEntries = new List<EvolutionStageEntry>();
                foreach (var kvp in _currentEvolutionStages)
                {
                    stageEntries.Add(new EvolutionStageEntry
                    {
                        eggId = kvp.Key,
                        stage = kvp.Value
                    });
                }

                var saveData = new EvolutionSaveData
                {
                    evolutionRequirements = requirementEntries.ToArray(),
                    evolutionHistory = historyEntries.ToArray(),
                    currentEvolutionStages = stageEntries.ToArray(),
                    saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string json = JsonUtility.ToJson(saveData, true);
                await System.IO.File.WriteAllTextAsync(GetSaveFilePath(), json);

                Debug.Log("[EvolutionService] 데이터 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvolutionService] 데이터 저장 중 오류: {e.Message}");
            }
        }

        public async UniTask LoadDataAsync()
        {
            try
            {
                string filePath = GetSaveFilePath();
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.Log("[EvolutionService] 저장된 데이터가 없습니다. 초기값으로 시작합니다.");
                    return;
                }

                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var saveData = JsonUtility.FromJson<EvolutionSaveData>(json);

                // 구조체 배열들을 Dictionary로 변환
                _evolutionRequirements = new Dictionary<int, EvolutionRequirement>();
                if (saveData.evolutionRequirements != null)
                {
                    foreach (var entry in saveData.evolutionRequirements)
                    {
                        if (entry == null)
                            continue;

                        _evolutionRequirements[entry.eggId] = entry.requirement;
                    }
                }

                _evolutionHistory = new Dictionary<int, List<EvolutionRecord>>();
                if (saveData.evolutionHistory != null)
                {
                    foreach (var entry in saveData.evolutionHistory)
                    {
                        if (entry == null)
                            continue;

                        var records = entry.records != null
                            ? new List<EvolutionRecord>(entry.records)
                            : new List<EvolutionRecord>();

                        _evolutionHistory[entry.eggId] = records;
                    }
                }

                _currentEvolutionStages = new Dictionary<int, int>();
                if (saveData.currentEvolutionStages != null)
                {
                    foreach (var entry in saveData.currentEvolutionStages)
                    {
                        if (entry == null)
                            continue;

                        _currentEvolutionStages[entry.eggId] = entry.stage;
                    }
                }

                Debug.Log("[EvolutionService] 데이터 로드 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvolutionService] 데이터 로드 중 오류: {e.Message}");
                _evolutionRequirements = new Dictionary<int, EvolutionRequirement>();
                _evolutionHistory = new Dictionary<int, List<EvolutionRecord>>();
                _currentEvolutionStages = new Dictionary<int, int>();
            }
        }

        private string GetSaveFilePath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "evolution_data.json");
        }

        [Serializable]
        private class EvolutionRequirement
        {
            public int requiredLevel;
        }

        [Serializable]
        private class EvolutionSaveData
        {
            public EvolutionRequirementEntry[] evolutionRequirements;
            public EvolutionHistoryEntry[] evolutionHistory;
            public EvolutionStageEntry[] currentEvolutionStages;
            public long saveTime;
        }

        [Serializable]
        private class EvolutionRequirementEntry
        {
            public int eggId;
            public EvolutionRequirement requirement;
        }

        [Serializable]
        private class EvolutionHistoryEntry
        {
            public int eggId;
            public EvolutionRecord[] records;
        }

        [Serializable]
        private class EvolutionStageEntry
        {
            public int eggId;
            public int stage;
        }
    }
}

/*
EvolutionService 구현

▶구현 내용:
ㆍ진화 조건 판정: 경험치 100% + 육성 행동 횟수 조건 확인
ㆍ진화 형태 결정: 스탯 기반 확률적 진화 형태 선택
ㆍ진화 확률 계산: 주요 스탯 값과 균형 보너스 고려
ㆍ진화 형태 데이터: 5가지 알 타입별 진화 형태 정의
ㆍ진화 이력 관리: 알별 진화 기록 저장 및 조회
ㆍ시뮬레이션 기능: 테스트용 진화 시뮬레이션
ㆍ이벤트 시스템: 진화 시도/완료/실패 시 EventBus 알림

▶핵심 기능:
ㆍ알 타입별 고유한 진화 형태 (빛의 알 → 온순한 빛/활발한 빛)
ㆍ스탯 기반 확률적 진화 (주요 스탯 + 균형 보너스)
ㆍ진화 후 알 데이터 초기화 (재육성 가능)
ㆍ진화 요구사항 커스터마이징 지원

*/



