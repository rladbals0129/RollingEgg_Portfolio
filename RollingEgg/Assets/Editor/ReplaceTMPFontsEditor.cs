#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RollingEgg.EditorTools
{
    /// <summary>
    /// 프로젝트 전체(현재 열린 씬 + 모든 프리팹)에 있는 TMP_Text 폰트를
    /// 지정한 TMP_FontAsset으로 일괄 교체하는 유틸리티
    /// </summary>
    public static class ReplaceTMPFontsEditor
    {
        // TODO: 필요시 경로나 에셋 이름만 수정해서 사용하세요.
        // 예: "Assets/01. Fonts/DNFBitBitv2_SDF_Fixed.asset"
        public const string TargetFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/DNFBitBitv2 SDF.asset";
        public const string CjkFontAssetPath    = "Assets/TextMesh Pro/Resources/Fonts & Materials/BoutiqueBitmap9x9_Bold_2.asset";

        [MenuItem("Tools/RollingEgg/UI/Replace All TMP Fonts To DNFBitBitv2", priority = 50)]
        public static void ReplaceAllTMPFonts()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetFontAssetPath);
            if (font == null)
            {
                Debug.LogError($"[ReplaceTMPFontsEditor] 타겟 폰트 에셋을 찾을 수 없습니다. 경로를 확인하세요.\nPath: {TargetFontAssetPath}");
                return;
            }

            int changedCount = 0;

            // 1) 현재 열린 씬들 내의 TMP_Text 모두 교체
            var sceneTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in sceneTexts)
            {
                if (text.font == font)
                    continue;

                Undo.RecordObject(text, "Replace TMP Font (Scene)");
                text.font = font;
                EditorUtility.SetDirty(text);
                changedCount++;
            }

            // 2) 프로젝트 내 모든 프리팹 안의 TMP_Text 교체
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string guid = prefabGuids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                bool prefabChanged = false;
                var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in texts)
                {
                    if (text.font == font)
                        continue;

                    Undo.RecordObject(text, "Replace TMP Font (Prefab)");
                    text.font = font;
                    EditorUtility.SetDirty(text);
                    changedCount++;
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ReplaceTMPFontsEditor] TMP_Text 폰트 일괄 교체 완료. 변경된 오브젝트 수: {changedCount}");
        }

        /// <summary>
        /// 모든 TMP_Text에 LocalizedTMPFont 컴포넌트를 자동 추가하고,
        /// 한국어/영어 폰트(DNFBitBitv2)와 CJK 폰트(BoutiqueBitmap9x9_Bold_2)를 기본값으로 세팅한다.
        /// </summary>
        [MenuItem("Tools/RollingEgg/UI/Add LocalizedTMPFont To All TMP_Text", priority = 51)]
        public static void AddLocalizedTMPFontToAllTMPTexts()
        {
            var koreanEnglishFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetFontAssetPath);
            var cjkFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontAssetPath);

            if (koreanEnglishFont == null || cjkFont == null)
            {
                Debug.LogError($"[ReplaceTMPFontsEditor] 폰트 에셋을 찾을 수 없습니다.\nKorean/English: {TargetFontAssetPath}\nCJK: {CjkFontAssetPath}");
                return;
            }

            int addedOrUpdatedCount = 0;

            // 1) 현재 열린 씬들 내의 TMP_Text 처리
            var sceneTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in sceneTexts)
            {
                addedOrUpdatedCount += AddOrUpdateLocalizedTMPFontOnText(text, koreanEnglishFont, cjkFont);
            }

            // 2) 모든 프리팹 내의 TMP_Text 처리
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string guid = prefabGuids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                bool prefabChanged = false;
                var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in texts)
                {
                    int result = AddOrUpdateLocalizedTMPFontOnText(text, koreanEnglishFont, cjkFont);
                    if (result > 0)
                    {
                        prefabChanged = true;
                        addedOrUpdatedCount += result;
                    }
                }

                if (prefabChanged)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ReplaceTMPFontsEditor] LocalizedTMPFont 자동 추가/업데이트 완료. 처리된 컴포넌트 수: {addedOrUpdatedCount}");
        }

        /// <summary>
        /// 단일 TMP_Text에 대해 LocalizedTMPFont를 추가하거나 기존 설정을 업데이트한다.
        /// </summary>
        private static int AddOrUpdateLocalizedTMPFontOnText(TMP_Text text, TMP_FontAsset koreanEnglishFont, TMP_FontAsset cjkFont)
        {
            if (text == null)
                return 0;

            var existing = text.GetComponent<RollingEgg.UI.LocalizedTMPFont>();
            if (existing == null)
            {
                Undo.RecordObject(text.gameObject, "Add LocalizedTMPFont");
                existing = text.gameObject.AddComponent<RollingEgg.UI.LocalizedTMPFont>();
            }
            else
            {
                Undo.RecordObject(existing, "Update LocalizedTMPFont");
            }

            var so = new SerializedObject(existing);
            var propKorean = so.FindProperty("koreanEnglishFont");
            var propCjk = so.FindProperty("cjkFont");

            bool changed = false;
            if (propKorean.objectReferenceValue != koreanEnglishFont)
            {
                propKorean.objectReferenceValue = koreanEnglishFont;
                changed = true;
            }

            if (propCjk.objectReferenceValue != cjkFont)
            {
                propCjk.objectReferenceValue = cjkFont;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(existing);
                return 1;
            }

            return 0;
        }
    }
}
#endif


