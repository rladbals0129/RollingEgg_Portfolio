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
    /// 도감 관리 서비스 구현체
    /// 진화체 도감 등록, 중복 처리, 버프 적용 기능을 제공
    /// </summary>
    public class CollectionService : ICollectionService
    {
        private HashSet<int> _registeredForms = new HashSet<int>();
        private Dictionary<int, EvolutionForm> _formCache = new Dictionary<int, EvolutionForm>();
        private readonly Dictionary<string, float> _specialCurrencyBuffs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private float _commonCurrencyBuffPercent;
        private float _duplicateRewardBuffPercent;

        private IEventBus _eventBus;
        private IResourceService _resourceService;
        private ICurrencyService _currencyService;
        private EvolvedFormTableSO _evolvedFormTable;

        // 재화 ID 상수 (알 타입별 전용 재화)
        private const int BLUE_CURRENCY_ID = 2;   // 파란색 재화
        private const int RED_CURRENCY_ID = 3;    // 빨간색 재화
        private const int YELLOW_CURRENCY_ID = 4; // 노란색 재화
        private const int WHITE_CURRENCY_ID = 5;  // 하얀색 재화
        private const int BLACK_CURRENCY_ID = 6;  // 검은색 재화

        public async UniTask InitializeAsync()
        {
            Debug.Log("[CollectionService] 초기화 시작...");

            // 서비스 의존성 주입
            _eventBus = ServiceLocator.Get<IEventBus>();
            _resourceService = ServiceLocator.Get<IResourceService>();
            _currencyService = ServiceLocator.Get<ICurrencyService>();

            // 테이블 로드
            _evolvedFormTable = await _resourceService.LoadAssetAsync<EvolvedFormTableSO>("EvolvedFormTableSO");
            if (_evolvedFormTable == null)
            {
                Debug.LogWarning("[CollectionService] EvolvedFormTableSO 로드 실패 또는 미등록");
                return;
            }

            // 폼 캐시 구성
            _formCache.Clear();
            foreach (var row in _evolvedFormTable.Rows)
            {
                if (row == null) continue;
                var form = new EvolutionForm
                {
                    formId = row.id,
                    formName = row.nameLocKey,
                    eggType = row.eggType,
                    primaryStat = StatType.Courage, // 기본값
                    requiredStatValue = 0,
                    probability = 0f,
                    description = "",
                    iconAddress = row.icon != null ? row.icon.AssetGUID : "",
                    unlockConditions = new string[0],
                    buffType = row.buffType,
                    buffValuePercent = row.buffValuePercent,
                    buffTargetEggType = row.buffTargetEggType
                };
                _formCache[row.id] = form;
            }

            // 데이터 로드
            await LoadDataAsync();

            // 도감 버프 적용
            ApplyCollectionBuffs();

            Debug.Log($"[CollectionService] 초기화 완료: 등록된 진화체 {_registeredForms.Count}개");
        }

        public bool RegisterEvolutionForm(int formId)
        {
            if (_registeredForms.Contains(formId))
            {
                // 중복 처리
                ProcessDuplicateForm(formId);
                return false; // 중복 등록은 실패로 처리
            }

            _registeredForms.Add(formId);

            // 도감 등록 이벤트 발행
            _eventBus.Publish(new CollectionFormRegisteredEvent
            {
                formId = formId,
                formName = GetFormInfo(formId).formName
            });

            // 버프 재적용
            ApplyCollectionBuffs();

            Debug.Log($"[CollectionService] 신규 진화체 도감 등록: ID={formId}");
            return true;
        }

        public bool IsFormRegistered(int formId)
        {
            return _registeredForms.Contains(formId);
        }

        public List<int> GetRegisteredForms(string eggType = null)
        {
            if (string.IsNullOrEmpty(eggType))
                return _registeredForms.ToList();

            return _registeredForms.Where(id =>
                _formCache.TryGetValue(id, out var form) && form.eggType.Equals(eggType, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public EvolutionForm GetFormInfo(int formId)
        {
            return _formCache.TryGetValue(formId, out var form) ? form : new EvolutionForm();
        }

        public int ProcessDuplicateForm(int formId)
        {
            var row = _evolvedFormTable.Rows.FirstOrDefault(r => r.id == formId);
            if (row == null) return 0;

            // 중복 시 재화 지급
            int currencyId = GetCurrencyIdForEggType(row.eggType);
            int baseRewardAmount = row.duplicateRewardSpecialCurrency;
            int rewardAmount = ApplyPercentBuff(baseRewardAmount, _duplicateRewardBuffPercent);

            if (rewardAmount > 0)
            {
                _currencyService.AddCurrency(currencyId, rewardAmount, "중복 진화체", -1);
            }

            // 중복 이벤트 발행
            _eventBus.Publish(new CollectionDuplicateFormEvent
            {
                formId = formId,
                currencyId = currencyId,
                amount = rewardAmount
            });

            Debug.Log($"[CollectionService] 중복 진화체 처리: ID={formId}, 재화={currencyId}, 수량={rewardAmount}");
            return rewardAmount;
        }

        private int GetCurrencyIdForEggType(string eggType)
        {
            // 알 타입에 따른 재화 ID 매핑
            return eggType.ToLower() switch
            {
                "blue" => BLUE_CURRENCY_ID,
                "red" => RED_CURRENCY_ID,
                "yellow" => YELLOW_CURRENCY_ID,
                "white" => WHITE_CURRENCY_ID,
                "black" => BLACK_CURRENCY_ID,
                _ => BLUE_CURRENCY_ID // 기본값
            };
        }

        public void ApplyCollectionBuffs()
        {
            _commonCurrencyBuffPercent = 0f;
            _duplicateRewardBuffPercent = 0f;
            _specialCurrencyBuffs.Clear();

            if (_evolvedFormTable == null || _evolvedFormTable.Rows == null)
                return;

            foreach (var formId in _registeredForms)
            {
                var row = _evolvedFormTable.Rows.FirstOrDefault(r => r.id == formId);
                if (row == null || row.buffType == EvolvedFormTableSO.BuffType.None || row.buffValuePercent <= 0f)
                    continue;

                AccumulateBuff(row);
            }

            Debug.Log($"[CollectionService] 버프 갱신 완료 - 공용:{_commonCurrencyBuffPercent:0.#}%, 전용:{_specialCurrencyBuffs.Count}종, 중복:{_duplicateRewardBuffPercent:0.#}%");
        }

        public async UniTask SaveDataAsync()
        {
            try
            {
                var saveData = new CollectionSaveData
                {
                    registeredForms = _registeredForms.ToArray(),
                    saveTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string json = JsonUtility.ToJson(saveData, true);
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, "collection_data.json");
                await System.IO.File.WriteAllTextAsync(filePath, json);

                Debug.Log("[CollectionService] 데이터 저장 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CollectionService] 데이터 저장 중 오류: {e.Message}");
            }
        }

        public async UniTask LoadDataAsync()
        {
            try
            {
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, "collection_data.json");
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.Log("[CollectionService] 저장된 도감 데이터가 없습니다. 초기값으로 시작합니다.");
                    return;
                }

                string json = await System.IO.File.ReadAllTextAsync(filePath);
                Debug.Log($"[CollectionService] 로드할 JSON 데이터: {json}");

                var saveData = JsonUtility.FromJson<CollectionSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogError("[CollectionService] JSON 파싱 실패. 저장된 데이터가 손상되었거나 형식이 잘못되었습니다.");
                    _registeredForms = new HashSet<int>();
                    return;
                }

                Debug.Log($"[CollectionService] 파싱된 데이터 - 등록된 진화체: {(saveData.registeredForms?.Length ?? 0)}개");

                _registeredForms = new HashSet<int>(saveData.registeredForms ?? new int[0]);

                Debug.Log($"[CollectionService] 데이터 로드 완료: 등록된 진화체 {_registeredForms.Count}개");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CollectionService] 데이터 로드 중 오류: {e.Message}");
                _registeredForms = new HashSet<int>();
            }
        }

        [System.Serializable]
        private class CollectionSaveData
        {
            public int[] registeredForms;
            public long saveTime;
        }

        public float GetCommonCurrencyBuffPercent() => _commonCurrencyBuffPercent;

        public float GetSpecialCurrencyBuffPercent(string eggType)
        {
            if (string.IsNullOrWhiteSpace(eggType))
                return 0f;

            return _specialCurrencyBuffs.TryGetValue(NormalizeEggType(eggType), out var value) ? value : 0f;
        }

        public float GetDuplicateRewardBuffPercent() => _duplicateRewardBuffPercent;

        private void AccumulateBuff(EvolvedFormTableSO.EvolvedFormRow row)
        {
            switch (row.buffType)
            {
                case EvolvedFormTableSO.BuffType.CommonCurrencyGain:
                    _commonCurrencyBuffPercent += row.buffValuePercent;
                    break;
                case EvolvedFormTableSO.BuffType.SpecialCurrencyGain:
                    AccumulateSpecialCurrencyBuff(row);
                    break;
                case EvolvedFormTableSO.BuffType.DuplicateRewardBonus:
                    _duplicateRewardBuffPercent += row.buffValuePercent;
                    break;
            }
        }

        private void AccumulateSpecialCurrencyBuff(EvolvedFormTableSO.EvolvedFormRow row)
        {
            string targetEggType = string.IsNullOrWhiteSpace(row.buffTargetEggType)
                ? row.eggType
                : row.buffTargetEggType;

            if (string.IsNullOrWhiteSpace(targetEggType))
                return;

            string key = NormalizeEggType(targetEggType);
            if (_specialCurrencyBuffs.TryGetValue(key, out var existingValue))
            {
                _specialCurrencyBuffs[key] = existingValue + row.buffValuePercent;
            }
            else
            {
                _specialCurrencyBuffs[key] = row.buffValuePercent;
            }
        }

        private static string NormalizeEggType(string eggType) =>
            string.IsNullOrWhiteSpace(eggType) ? string.Empty : eggType.Trim().ToLowerInvariant();

        private static int ApplyPercentBuff(int baseValue, float percent)
        {
            if (baseValue <= 0 || Math.Abs(percent) <= float.Epsilon)
                return baseValue;

            float multiplier = 1f + percent * 0.01f;
            return Mathf.FloorToInt(baseValue * multiplier);
        }
    }
}
