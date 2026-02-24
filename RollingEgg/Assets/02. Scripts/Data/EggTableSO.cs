using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.Data
{
    [CreateAssetMenu(
        fileName = "EggTableSO",
        menuName = "RollingEgg/Data/Egg Table",
        order = 0)]
    public class EggTableSO : ScriptableObject
    {
        [Serializable]
        public class EggRow
        {
            [Tooltip("고유 ID (1=blue, 2=white, 3=black, 4=red, 5=yellow)")]
            public int id;
            
            [Tooltip("알 타입 문자열 (blue, white, black, red, yellow)")]
            public string eggType;
            
            [Tooltip("알 이름(로컬라이즈 키)")]
            public string nameLocKey;
            
            [Tooltip("알 아이콘(스프라이트) Addressables 참조")]
            public AssetReferenceSprite  icon;
            
            [Tooltip("알 이미지(육성/로비용) Addressables 참조")]
            public AssetReferenceSprite  eggImage;
            
            [Tooltip("알 애니메이션 컨트롤러(UI용) Addressables 참조")]
            public AssetReference animatorController;
            
            [Tooltip("맵 배경 이미지 Addressables 참조")]
            public AssetReferenceSprite mapBackground;
            
            [TextArea]
            [Tooltip("설명 텍스트(로컬라이즈 키로 대체 가능)")]
            public string description;
            
            [Tooltip("테마 색상")]
            public Color themeColor = Color.white;
        }
        
        [SerializeField]
        private List<EggRow> rows = new List<EggRow>();
        
        public IReadOnlyList<EggRow> Rows => rows;
        
        public bool TryGetById(int id, out EggRow row)
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
        
        public bool TryGetByType(string eggType, out EggRow row)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].eggType, eggType, StringComparison.OrdinalIgnoreCase))
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
            var typeSet = new HashSet<string>();
            
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                
                // ID 중복 체크
                if (!idSet.Add(row.id))
                {
                    Debug.LogWarning($"[EggTableSO] 중복된 id: {row.id}", this);
                }
                
                // 타입 중복 체크
                if (!string.IsNullOrEmpty(row.eggType))
                {
                    if (!typeSet.Add(row.eggType.ToLower()))
                    {
                        Debug.LogWarning($"[EggTableSO] 중복된 eggType: {row.eggType} (id={row.id})", this);
                    }
                }
                
                // 필수 필드 체크
                if (string.IsNullOrEmpty(row.eggType))
                {
                    Debug.LogWarning($"[EggTableSO] eggType이 비어있음: id={row.id}", this);
                }
            }
        }
#endif
    }
}

