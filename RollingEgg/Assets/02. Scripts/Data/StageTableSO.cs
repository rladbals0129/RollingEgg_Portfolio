using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Data
{
    [CreateAssetMenu(
        fileName = "StageTableSO",
        menuName = "RollingEgg/Data/Stage Table",
        order = 0)]
    public class StageTableSO : ScriptableObject
    {
        [Serializable]
        public class StageRow
        {
            [Tooltip("고유 ID")]
            public int id;

            [Tooltip("스테이지 배경 프리팹")]
            public GameObject background;

            [Tooltip("스테이지 진행에 사용할 맵 프리팹")]
            public GameObject mapPrefab;

            [Tooltip("연결될 EggTableSO의 id")]
            public int chapterId;

            [Tooltip("스테이지 번호")]
            public int stageNumber;

            [Tooltip("필요 키 개수")]
            public int keyCount;

            [Tooltip("사용 키 목록 (예: F, J 또는 D,F,J)")]
            public List<string> keys = new List<string>();

            [Tooltip("오픈 조건이 되는 Stage ID (0이면 즉시 오픈)")]
            public int openStageId;

            [Tooltip("스테이지 난이도")]
            public int level;

            [Tooltip("스테이지 고정 속도")]
            public float speed = 1f;

            [Tooltip("스테이지 고정 체력")]
            public float hp = 100f;

            [Tooltip("기본 체력 감소량")]
            public float reduceHP = 1f;

            [Tooltip("Perfect 판정 시 체력 변화량")]
            public float perfectHP = 0f;

            [Tooltip("Great 판정 시 체력 변화량")]
            public float greatHP = 0f;

            [Tooltip("Good 판정 시 체력 변화량")]
            public float goodHP = 0f;

            [Tooltip("Bad 판정 시 체력 변화량")]
            public float badHP = 0f;

            [Tooltip("Miss 판정 시 체력 변화량")]
            public float missHP = 0f;

            [Tooltip("키 색상 변경 쿨타임 (초)")]
            public float changeColorCooltime = 0f;

#if UNITY_EDITOR
            [HideInInspector]
            public int cachedKeyCount;
#endif
        }

        [SerializeField]
        private List<StageRow> rows = new List<StageRow>();

        public IReadOnlyList<StageRow> Rows => rows;

        public bool TryGetById(int id, out StageRow row)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id)
                {
                    row = rows[i];
                    return true;
                }
            }

            row = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var idSet = new HashSet<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                var stage = rows[i];
                if (stage == null)
                    continue;

                if (!idSet.Add(stage.id))
                {
                    Debug.LogWarning($"[StageTableSO] 중복된 id: {stage.id}", this);
                }

                NormalizeKeyData(stage);
            }
        }

        private static void NormalizeKeyData(StageRow stage)
        {
            stage.keyCount = Mathf.Max(0, stage.keyCount);
            stage.keys ??= new List<string>();

            if (stage.cachedKeyCount != stage.keyCount)
            {
                ResizeKeyList(stage);
            }
            else if (stage.keys.Count != stage.keyCount)
            {
                stage.keyCount = stage.keys.Count;
            }

            stage.cachedKeyCount = stage.keyCount;

            for (int i = 0; i < stage.keys.Count; i++)
            {
                var value = stage.keys[i];
                stage.keys[i] = string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : value.Trim().ToUpperInvariant();
            }
        }

        private static void ResizeKeyList(StageRow stage)
        {
            var targetCount = stage.keyCount;
            while (stage.keys.Count < targetCount)
            {
                stage.keys.Add(string.Empty);
            }

            if (stage.keys.Count > targetCount)
            {
                stage.keys.RemoveRange(targetCount, stage.keys.Count - targetCount);
            }
        }
#endif
    }
}

