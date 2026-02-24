using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.Data
{
    [CreateAssetMenu(
        fileName = "EvolvedFormTableSO",
        menuName = "RollingEgg/Data/Evolved Form Table",
        order = 0)]
    public class EvolvedFormTableSO : ScriptableObject
    {
        public enum BuffType
        {
            None,
            CommonCurrencyGain,     // 공용 재화 획득량 증가
            SpecialCurrencyGain,    // 지정 알 타입 전용 재화 획득량 증가
            DuplicateRewardBonus    // 중복 진화체 획득 시 지급 재화 증가
        }

        [Serializable]
        public class EvolvedFormRow
        {
            [Tooltip("고유 ID")] public int id;
            [Tooltip("진화체 명칭(로컬라이즈 키)")] public string nameLocKey;
            [Tooltip("알 타입 문자열(예: blue, red, yellow)")] public string eggType;
            [Tooltip("등급(1~5)")] [Range(1, 5)] public int grade = 1;
            [Tooltip("중복 시 지급 전용 재화 수량")] public int duplicateRewardSpecialCurrency;
            [Tooltip("아이콘 Addressables 참조")] public AssetReferenceSprite icon;
            [Tooltip("도감 등록 시 버프 타입")] public BuffType buffType = BuffType.None;
            [Tooltip("버프 적용 대상 알 타입(전용 재화 버프 전용)")] public string buffTargetEggType;
            [Tooltip("버프 수치(%)")] [Range(0f, 100f)] public float buffValuePercent = 0f;
        }

        [SerializeField]
        private List<EvolvedFormRow> rows = new List<EvolvedFormRow>();

        public IReadOnlyList<EvolvedFormRow> Rows => rows;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var idSet = new HashSet<int>();
            foreach (var r in rows)
            {
                if (r == null) continue;
                if (!idSet.Add(r.id))
                    Debug.LogWarning($"[EvolvedFormTableSO] 중복된 id: {r.id}", this);
                if (r.grade < 1 || r.grade > 5)
                    Debug.LogWarning($"[EvolvedFormTableSO] grade 범위(1~5)를 벗어남: id={r.id}", this);
            }
        }
#endif
    }
}
