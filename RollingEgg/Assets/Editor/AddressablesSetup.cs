#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RollingEgg.EditorTools
{
    /// <summary>
    /// Addressables 데이터 키를 자동으로 설정하는 유틸리티
    /// - GrowthConditionPoolSO, CurrencyTableSO를 Addressable로 등록하고 주소를 지정
    /// </summary>
    public static class AddressablesSetup
    {
        private const string GrowthPath = "Assets/06. ScriptableObject/Data/GrowthConditionPoolSO.asset";
        private const string GrowthKey = "GrowthConditionPoolSO";

        private const string CurrencyPath = "Assets/06. ScriptableObject/Data/CurrencyTableSO.asset";
        private const string CurrencyKey = "CurrencyTableSO";

        private const string EvolutionRulePath = "Assets/06. ScriptableObject/Data/EvolutionRuleTableSO.asset";
        private const string EvolutionRuleKey = "EvolutionRuleTableSO";

        private const string EvolvedFormPath = "Assets/06. ScriptableObject/Data/EvolvedFormTableSO.asset";
        private const string EvolvedFormKey = "EvolvedFormTableSO";

        private const string EggTablePath = "Assets/06. ScriptableObject/Data/EggTableSO.asset";
        private const string EggTableKey = "EggTableSO";

        private const string StatTypePath = "Assets/06. ScriptableObject/Data/StatTypeSO.asset";
        private const string StatTypeKey = "StatTypeSO";

        private const string StageTablePath = "Assets/06. ScriptableObject/Data/StageTableSO.asset";
        private const string StageTableKey = "StageTableSO";

        [MenuItem("Tools/RollingEgg/Addressables/Setup Data Keys", priority = 10)]
        public static void SetupDataKeys()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] AddressableAssetSettings 를 생성/가져올 수 없습니다.");
                return;
            }

            // 기본 그룹 보장
            var group = settings.DefaultGroup;
            if (group == null)
            {
                group = settings.CreateGroup("Default Local Group", false, false, false, null);
                settings.DefaultGroup = group;
            }

            // 라벨 보장
            settings.AddLabel("Data", true);

            // 등록 함수
            void AddOrUpdate(string assetPath, string address)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning($"[AddressablesSetup] 에셋을 찾을 수 없습니다: {assetPath}");
                    return;
                }

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                    entry = settings.CreateOrMoveEntry(guid, group);

                if (entry.address != address)
                    entry.address = address;

                entry.SetLabel("Data", true, true);
            }

            AddOrUpdate(GrowthPath, GrowthKey);
            AddOrUpdate(CurrencyPath, CurrencyKey);
            AddOrUpdate(EvolutionRulePath, EvolutionRuleKey);
            AddOrUpdate(EvolvedFormPath, EvolvedFormKey);
            AddOrUpdate(EggTablePath, EggTableKey);
            AddOrUpdate(StatTypePath, StatTypeKey);
            AddOrUpdate(StageTablePath, StageTableKey);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[AddressablesSetup] 데이터 키 설정 완료: GrowthConditionPoolSO, CurrencyTableSO, EvolutionRuleTableSO, EvolvedFormTableSO, EggTableSO, StatTypeSO");
        }

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            // 에디터 세션에서 한 번만 자동 적용
            if (SessionState.GetBool("RE_AutoAddrSetupDone", false))
                return;

            try
            {
                SetupDataKeys();
                SessionState.SetBool("RE_AutoAddrSetupDone", true);
            }
            catch
            {
                // 컴파일 직후 Addressables 세팅이 아직 준비되지 않았을 수 있으므로 조용히 무시
            }
        }
    }
}
#endif


