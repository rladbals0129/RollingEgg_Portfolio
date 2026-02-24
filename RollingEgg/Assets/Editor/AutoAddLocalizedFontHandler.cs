using UnityEditor;
using UnityEngine;
using TMPro;
using RollingEgg.UI;

namespace RollingEgg.EditorTools
{
    /// <summary>
    /// 에디터에서 TMP_Text 컴포넌트가 추가될 때, 자동으로 LocalizedTMPFont를 함께 추가해주는 핸들러
    /// </summary>
    [InitializeOnLoad]
    public static class AutoAddLocalizedFontHandler
    {
        static AutoAddLocalizedFontHandler()
        {
            // 1. 컴포넌트가 인스펙터나 스크립트(ObjectFactory 경유)로 추가될 때 감지
            ObjectFactory.componentWasAdded += HandleComponentAdded;
            
            // 2. Hierarchy 창에서 우클릭 > UI > Text - TMP 등으로 생성될 때 감지
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        }

        private static void HandleComponentAdded(Component component)
        {
            TryAddLocalizedFont(component.gameObject);
        }

        private static void HandleHierarchyChanged()
        {
            // Hierarchy 변경 시, 현재 선택된 오브젝트가 방금 생성된 TMP 오브젝트일 가능성이 높음
            if (Selection.activeGameObject != null)
            {
                TryAddLocalizedFont(Selection.activeGameObject);
            }
        }

        private static void TryAddLocalizedFont(GameObject go)
        {
            // TMP_Text가 없으면 무시
            if (go.GetComponent<TMP_Text>() == null)
                return;

            // 이미 LocalizedTMPFont가 있으면 무시
            if (go.GetComponent<LocalizedTMPFont>() != null)
                return;

            // LocalizedTMPFont 추가
            // Undo 등록을 위해 Undo.AddComponent 사용 권장 (이미 생성된 오브젝트에 대해)
            var localizedFont = Undo.AddComponent<LocalizedTMPFont>(go);
            
            // 폰트 에셋 로드 (ReplaceTMPFontsEditor의 상수 활용)
            var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ReplaceTMPFontsEditor.TargetFontAssetPath);
            var cjkFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ReplaceTMPFontsEditor.CjkFontAssetPath);

            if (koreanFont != null && cjkFont != null)
            {
                var so = new SerializedObject(localizedFont);
                so.FindProperty("koreanEnglishFont").objectReferenceValue = koreanFont;
                so.FindProperty("cjkFont").objectReferenceValue = cjkFont;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            
            Debug.Log($"[AutoAddLocalizedFont] '{go.name}'에 LocalizedTMPFont가 자동 추가되었습니다.");
        }
    }
}





