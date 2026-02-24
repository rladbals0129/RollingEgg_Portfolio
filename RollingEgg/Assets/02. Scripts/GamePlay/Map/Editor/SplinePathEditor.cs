using UnityEditor;
using UnityEngine;

namespace RollingEgg
{
    [CustomEditor(typeof(SplinePath))]
    public class SplinePathEditor : Editor
    {
        private SplinePath _splinePath;

        private void OnEnable()
        {
            _splinePath = (SplinePath)target;
        }

        public override void OnInspectorGUI()
        {
            // 변경 감지 시작
            EditorGUI.BeginChangeCheck();

            // 기본 인스펙터 그리기
            DrawDefaultInspector();
            GUILayout.Space(10);

            // 변경이 있으면 SplinePath 업데이트
            if (EditorGUI.EndChangeCheck())
            {
                // 1. Undo/Redo(Ctrl+Z) 기능을 위해 변경을 기록합니다.
                Undo.RecordObject(_splinePath, "Spline Path Property Changed");

                // 2. 데이터를 업데이트하는 메인 함수를 호출합니다.
                _splinePath.HandleSplineUpdate();

                // 3. Unity에게 이 오브젝트가 변경되어 저장해야 한다고 명시적으로 알립니다. (매우 중요!)
                EditorUtility.SetDirty(_splinePath);

                // 4. Scene 뷰를 강제로 다시 그리도록 명령합니다.
                SceneView.RepaintAll();
            }

            // Spline 정보 표시
            if (_splinePath.SplineContainer != null)
            {
                EditorGUILayout.LabelField("SplinePath 옵션", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");

                EditorGUI.BeginDisabledGroup(true);

                // SplineContainer와 Spline의 null 체크 추가
                if (_splinePath.SplineContainer != null && _splinePath.SplineContainer.Spline != null)
                {
                    int knotCount = _splinePath.SplineContainer.Spline.Count;
                    EditorGUILayout.LabelField("Knot Count: " + knotCount);
                }
                else
                {
                    EditorGUILayout.LabelField("Knot Count: 0 (Spline이 초기화되지 않음)", EditorStyles.helpBox);
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}