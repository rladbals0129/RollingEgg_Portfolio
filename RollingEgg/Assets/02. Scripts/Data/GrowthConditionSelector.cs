using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RollingEgg.Data
{
	public static class GrowthConditionSelector
	{
		// recentHistory: 최근 등장한 id들(쿨다운용), seed: 재현성 보장용
		public static List<GrowthConditionPoolSO.ConditionEntry> Pick(
			GrowthConditionPoolSO pool, int level, int count,
			IReadOnlyCollection<int> recentHistory, int seed)
		{
			var candidates = new List<GrowthConditionPoolSO.ConditionEntry>();
			for (int i = 0; i < pool.Rows.Count; i++)
			{
				var e = pool.Rows[i];
				if (e == null) continue;
				if (e.minLevel <= level && level <= e.maxLevel && e.weight > 0f)
				{
					if (e.cooldownTurns > 0 && recentHistory != null && recentHistory.Contains(e.id))
						continue;

					candidates.Add(e);
				}
			}

			var picked = new List<GrowthConditionPoolSO.ConditionEntry>(Mathf.Max(0, count));
			var rnd = new System.Random(seed);

			while (picked.Count < count && candidates.Count > 0)
			{
				var chosen = WeightedPick(candidates, rnd);
				if (chosen == null) break;

				picked.Add(chosen);

				// 비복원 + 상호 배타 제거
				candidates.Remove(chosen);
				if (chosen.exclusiveWith != null && chosen.exclusiveWith.Count > 0)
				{
					candidates.RemoveAll(c => chosen.exclusiveWith.Contains(c.id));
				}
			}

			return picked;
		}

		private static GrowthConditionPoolSO.ConditionEntry WeightedPick(
			List<GrowthConditionPoolSO.ConditionEntry> list, System.Random rnd)
		{
			float sum = 0f;
			for (int i = 0; i < list.Count; i++) sum += Mathf.Max(0f, list[i].weight);
			if (sum <= 0f) return null;

			var roll = (float)rnd.NextDouble() * sum;
			float acc = 0f;
			for (int i = 0; i < list.Count; i++)
			{
				acc += Mathf.Max(0f, list[i].weight);
				if (roll <= acc) return list[i];
			}
			return list[list.Count - 1];
		}
	}
}


