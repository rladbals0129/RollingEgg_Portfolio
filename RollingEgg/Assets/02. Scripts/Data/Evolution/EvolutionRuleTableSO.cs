using System;
using System.Collections.Generic;
using UnityEngine;


namespace RollingEgg.Data
{
    [CreateAssetMenu(
        fileName = "EvolutionRuleTableSO",
        menuName = "RollingEgg/Data/Evolution Rule Table",
        order = 0)]
    public class EvolutionRuleTableSO : ScriptableObject
    {
        [Serializable]
        public class FormProbability
        {
            [Tooltip("진화체 ID (EvolvedFormTableSO의 id)")]
            public int formId;

            [Range(0f, 100f)]
            [Tooltip("진화 확률(%)")]
            public float probabilityPercent = 0f;
        }

        [Serializable]
        public class EvolutionRuleRow
        {
            [Tooltip("고유 번호 (예: 1,2,3,...)")]
            public int id;

            [Tooltip("알 타입 문자열 (예: blue, red, yellow ...)")]
            public string eggType;

            [Tooltip("지배 스탯 타입")]
           public RollingEgg.Data.StatType dominantStat;

            [Tooltip("최소 육성 레벨 (조건) ")]
            public int minNurtureLevel = 1;

            [Tooltip("가능한 진화 결과 및 확률")]
            public List<FormProbability> outcomes = new List<FormProbability>();
        }

        [SerializeField]
        private List<EvolutionRuleRow> rows = new List<EvolutionRuleRow>();

        public IReadOnlyList<EvolutionRuleRow> Rows => rows;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var idSet = new HashSet<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r == null) continue;

                if (!idSet.Add(r.id))
                    Debug.LogWarning($"[EvolutionRuleTableSO] 중복된 id: {r.id}", this);

                float sum = 0f;
                for (int j = 0; j < r.outcomes.Count; j++)
                {
                    if (r.outcomes[j] == null) continue;
                    sum += Mathf.Max(0f, r.outcomes[j].probabilityPercent);
                }
                if (Mathf.Abs(sum - 100f) > 0.01f)
                    Debug.LogWarning($"[EvolutionRuleTableSO] 확률 합이 100%가 아님 (id={r.id}, sum={sum:F2}%)", this);

                if (r.minNurtureLevel < 1)
                    Debug.LogWarning($"[EvolutionRuleTableSO] minNurtureLevel은 1 이상이어야 합니다. (id={r.id})", this);
            }
        }
#endif
    }
}


