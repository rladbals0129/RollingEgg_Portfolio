using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.Data
{
	[CreateAssetMenu(
		fileName = "GrowthConditionPoolSO",
		menuName = "RollingEgg/Data/Growth Condition Pool",
		order = 0)]
	public class GrowthConditionPoolSO : ScriptableObject
	{
		[Serializable]
		public enum Rarity
		{
			Common,
			Rare,
			Epic
		}

		// StatType 열거형은 중앙화된 RollingEgg.Data.StatType 사용

		[Serializable]
		public class StatDelta
		{
			[Tooltip("증가/감소 대상 스탯")]
			public StatType stat;

			[Tooltip("증가(+) 또는 감소(-) 수치")]
			public int amount = 1;
		}

		[Serializable]
		public class ConditionEntry
		{
			[Tooltip("고유 번호 (예: 1,2,3,...)")]
			public int id;

			[Tooltip("표시명 로컬라이즈 키(또는 설명)")]
			public string nameLocKey;

			[Header("비용")]
			[Tooltip("공용 재화 소모")]
			public int costCommon;

			[Tooltip("전용 재화 소모")]
			public int costSpecial;

			[Tooltip("이 조건을 수행할 때 변화하는 스탯 목록")]
			public List<StatDelta> statIncrements = new List<StatDelta>();

			[Header("출력 대사")]
			[Tooltip("행동 수행 시 출력 대사 키(로컬라이즈)#1")]
			public string line1;

			[Tooltip("행동 수행 시 출력 대사 키(로컬라이즈)#2")]
			public string line2;

			[Tooltip("행동 수행 시 출력 대사 키(로컬라이즈)#3")]
			public string line3;

			[Header("노출 조건")]
			[Tooltip("이 조건이 등장 가능한 최소 레벨")]
			public int minLevel = 1;

			[Tooltip("이 조건이 등장 가능한 최대 레벨")]
			public int maxLevel = 99;

			[Tooltip("가중치(확률 가중). 레벨별 가중치와 곱연산될 수 있음")]
			public float weight = 1f;

			[Tooltip("희귀도. 레벨별 희귀도 정책에 따라 가중치가 달라질 수 있음")]
			public Rarity rarity = Rarity.Common;

			[Tooltip("시각 요소가 필요하면 아이콘 참조(선택)")]
			public AssetReferenceSprite icon;

			[Header("제약")]
			[Tooltip("함께 등장하면 안 되는 조건 id 목록")]
			public List<int> exclusiveWith = new List<int>();

			[Tooltip("최근 N회(세션 내) 등장 금지. 0이면 미사용")]
			public int cooldownTurns = 0;
		}

		[SerializeField]
		[Tooltip("성장 조건(오퍼)의 풀. 여기서 레벨/가중치 규칙으로 5개를 뽑아 UI에 노출합니다.")]
		private List<ConditionEntry> rows = new List<ConditionEntry>();

		public IReadOnlyList<ConditionEntry> Rows => rows;

#if UNITY_EDITOR
		private void OnValidate()
		{
			var idSet = new HashSet<int>();
			for (int i = 0; i < rows.Count; i++)
			{
				var e = rows[i];

				if (!idSet.Add(e.id))
					Debug.LogWarning($"[GrowthConditionPoolSO] 중복된 id: {e.id}", this);

				if (e.minLevel > e.maxLevel)
					Debug.LogWarning($"[GrowthConditionPoolSO] minLevel > maxLevel : id={e.id}", this);

				if (e.weight <= 0f)
					Debug.LogWarning($"[GrowthConditionPoolSO] weight가 0 이하: id={e.id}", this);
			}
		}
#endif
	}
}


