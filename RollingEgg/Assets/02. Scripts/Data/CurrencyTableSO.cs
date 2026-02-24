using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.Data
{

    [CreateAssetMenu(
        fileName = "CurrencyTableSO",
        menuName = "RollingEgg/Data/Currency Table",
        order = 0)]
    public class CurrencyTableSO : ScriptableObject
    {
        [Serializable]
        public class CurrencyRow
        {
            [Tooltip("고유 번호 (예: 1,2,3,...)")]
            public int id;
            [Tooltip("재화 사용 pc 타입 / 0 = 공용, 혹은 egg type 문자열(yellow/blue/...)")]
            public string type;

          	[Tooltip("희귀도 배율 (획득량에 곱함)")]
			public float rarity = 1f;

			[Tooltip("아이콘(스프라이트) Addressables 참조")]
			public AssetReferenceSprite icon;

			[TextArea]
			[Tooltip("설명 텍스트(로컬라이즈 키로 대체 가능)")]
			public string description;
        }

        
		[SerializeField]
		private List<CurrencyRow> rows = new List<CurrencyRow>();

		public IReadOnlyList<CurrencyRow> Rows => rows;

		public bool TryGetById(int id, out CurrencyRow row)
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
			var set = new HashSet<int>();
			for (int i = 0; i < rows.Count; i++)
			{
				if (!set.Add(rows[i].id))
				{
					Debug.LogWarning($"[CurrencyTableSO] 중복된 Id 발견: {rows[i].id}", this);
				}
			}
		}
#endif






    }
}
