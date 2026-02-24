using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Data
{
    /// <summary>
    /// 스탯 메타데이터 정의(SO). 색상, 표시명 등 확장이 필요할 때 사용.
    /// StatType enum은 별도 파일(StatType.cs)에 정의되어 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "StatTypeSO",
        menuName = "RollingEgg/Data/Stat Type",
        order = 0)]
    public class StatTypeSO : ScriptableObject
    {
        [Serializable]
        public class StatInfo
        {
            [Tooltip("스탯 타입")] public StatType type;
            [Tooltip("표시명 로컬라이즈 키")] public string nameLocKey;
    
            [Tooltip("표시 색상")] public Color color = Color.white;
        }

        [SerializeField]
        [Tooltip("스탯 메타데이터 목록(5개)")]
        private List<StatInfo> stats = new List<StatInfo>();

        public IReadOnlyList<StatInfo> Stats => stats;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 중복 타입 경고 및 개수 안내
            var set = new HashSet<StatType>();
            foreach (var s in stats)
            {
                if (s == null) continue;
                if (!set.Add(s.type))
                    Debug.LogWarning($"[StatTypeSO] 중복 타입: {s.type}", this);
            }

            if (stats.Count != 5)
                Debug.LogWarning($"[StatTypeSO] 권장 항목 수는 5개입니다. 현재 {stats.Count}개", this);
        }
#endif
    }
}


