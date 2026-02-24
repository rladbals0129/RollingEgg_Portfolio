using UnityEditor;
using UnityEngine;

namespace RollingEgg
{
    [CustomEditor(typeof(SafeZone))]
    public class SafeZoneEditor : Editor
    {
        private SafeZone _safeZone;

        private void OnEnable()
        {
            _safeZone = (SafeZone)target;
        }

        public override void OnInspectorGUI()
        {
            // 변경 감지 시작
            EditorGUI.BeginChangeCheck();

            // 기본 인스펙터 그리기
            DrawDefaultInspector();
            GUILayout.Space(10);

            // 변경이 있으면 SafeZone 업데이트
            if (EditorGUI.EndChangeCheck())
            {
                // Undo/Redo(Ctrl+Z) 기능을 위해 변경을 기록
                Undo.RecordObject(_safeZone, "SafeZone Property Changed");

                // 데이터를 업데이트하는 메인 함수를 호출합니다.
                _safeZone.HandleLiveUpdate();

                EditorUtility.SetDirty(_safeZone);

                SceneView.RepaintAll();
            }
        }
    }
}
