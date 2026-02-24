using RollingEgg.Util;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace RollingEgg
{
    public struct ButtonStyle
    {
        public Color TextColor;
        public Color BackgroundColor;
        public Color SelectedTextColor;
        public Color SelectedBackgroundColor;
        public bool BoldWhenSelected;
        public bool BorderWhenSelected;
        public Color BorderColor;

        // 기본 스타일을 제공하는 정적 메서드
        public static ButtonStyle Default()
        {
            return new ButtonStyle
            {
                TextColor = GUI.skin.button.normal.textColor,
                BackgroundColor = GUI.backgroundColor,
                SelectedTextColor = Color.white,
                SelectedBackgroundColor = new Color(0.3f, 0.7f, 1f), // 선택 시 파란색
                BoldWhenSelected = true,
                BorderWhenSelected = true, // 선택 시 테두리 사용
                BorderColor = Color.white
            };
        }
    }

    public class SplinePathCreator : EditorWindow
    {
        private const string SPLINEPATH_PREFAB_PATH = "Assets/03. Prefabs/Map/SplinePath.prefab";
        private const string SAFEZONE_PREFAB_PATH = "Assets/03. Prefabs/Map/SafeZone.prefab";
        private const string ENDPOINT_PREFAB_PATH = "Assets/03. Prefabs/Map/EndPoint.prefab";
        private const string DEFAULT_PARENT_NAME = "Map";

        private enum ETabType
        {
            MapSetting,
            SplinePath,
            SafeZone,
            OtherObjects,
        }

        private enum EDirection { N, NE, E, SE, S, SW, W, NW }
        private enum EBranchBasePoint { Start, End }

        private ETabType _currentTab = ETabType.MapSetting;
        private string[] _tabNames = { "Map Settings", "Spline Path", "Safe Zone", "Other Objects" };

        [Header("Map Settings")]
        private string _mapName = "New Map";
        private Transform _rootTransform;

        [Header("SplinePath Settings")]
        private GameObject _splinePathPrefab;
        private EDirection _direction = EDirection.E;
        private float _length = 2f;

        [Header("SafeZone Settings")]
        private GameObject _safeZonePrefab;
        private Transform _safeZoneRoot;
        private float _radius;

        [Header("EndPoint Settings")]
        private GameObject _endPointPrefab;

        private SplinePath _splinePathForSafeZone;
        private TextAsset _safeZoneCSVFile;

        // UI 스타일
        private GUIStyle _tabStyle;
        private GUIStyle _selectedTabStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _boxStyle;

        private bool _isStyleInitialized;

        [MenuItem("Tools/Map Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<SplinePathCreator>("Map Creator");
            window.minSize = new Vector2(400, 600);
            window.maxSize = new Vector2(800, 1000);
        }

        private void OnEnable()
        {
            // Selection이 변경될 때마다 윈도우를 다시 그리도록 이벤트 등록
            Selection.selectionChanged += Repaint;

            // Prefab 로드
            _splinePathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SPLINEPATH_PREFAB_PATH);
            _safeZonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SAFEZONE_PREFAB_PATH);
            _endPointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENDPOINT_PREFAB_PATH);

            _isStyleInitialized = false;
        }

        private void OnDisable()
        {
            // 이벤트 해제
            Selection.selectionChanged -= Repaint;
        }

        private void InitializeStyles()
        {
            _tabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 30,
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };

            _selectedTabStyle = new GUIStyle(_tabStyle)
            {
                normal = { background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn left on.png") as Texture2D },
                fontStyle = FontStyle.Bold
            };

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
        }

        private void OnGUI()
        {
            if (!_isStyleInitialized)
            {
                InitializeStyles();
                _isStyleInitialized = true;
            }

            EditorGUILayout.BeginVertical();

            // 탭 헤더
            DrawTabHeader();

            EditorGUILayout.Space(10);

            // 탭 내용
            DrawTabContent();

            EditorGUILayout.EndVertical();
        }

        private void DrawTabHeader()
        {
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _tabNames.Length; i++)
            {
                ETabType tabType = (ETabType)i;
                bool isSelected = _currentTab == tabType;

                if (GUILayout.Button(_tabNames[i], isSelected ? _selectedTabStyle : _tabStyle))
                {
                    _currentTab = tabType;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabContent()
        {
            switch (_currentTab)
            {
                case ETabType.MapSetting:
                    DrawMapSettingTab();
                    break;
                case ETabType.SplinePath:
                    DrawSplinePathTab();
                    break;
                case ETabType.SafeZone:
                    DrawSafeZoneTab();
                    break;
                case ETabType.OtherObjects:
                    DrawOtherObjectsTab();
                    break;
            }
        }

        #region MapSetting Tab
        private void DrawMapSettingTab()
        {
            EditorGUILayout.LabelField("MapData 기본값 설정", _sectionStyle);
            EditorGUILayout.BeginVertical(_boxStyle);

            // 맵 기본 정보
            _mapName = EditorGUILayout.TextField("Map Name", _mapName);
            EditorGUILayout.Space(5);

            // 루트 Transform 설정
            EditorGUILayout.LabelField("Transform Settings", EditorStyles.boldLabel);
            _rootTransform = (Transform)EditorGUILayout.ObjectField("Root Transform", _rootTransform, typeof(Transform), true);

            if (_rootTransform == null)
            {
                EditorGUILayout.HelpBox("Root Transform이 설정되지 않았습니다. 맵 오브젝트를 선택하거나 새로 생성하세요.", MessageType.Warning);

                if (GUILayout.Button("새로운 루트 맵 생성"))
                {
                    CreateMapRoot();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateMapRoot()
        {
            GameObject mapRoot = new GameObject(_mapName);
            _rootTransform = mapRoot.transform;

            CreateSplinePath();

            GameObject safeZoneRoot = new GameObject("SafeZone_Root");
            safeZoneRoot.transform.SetParent(_rootTransform);
            _safeZoneRoot = safeZoneRoot.transform;

            Selection.activeGameObject = mapRoot;
            Debug.Log($"맵 루트 생성: {_mapName}");
        }

        private void CreateSplinePath()
        {
            if (_splinePathPrefab == null)
            {
                EditorUtility.DisplayDialog("오류", $"SplinePath Prefab이 할당되어 있지 않습니다.", "확인");
                return;
            }

            GameObject splineObj = PrefabUtility.InstantiatePrefab(_splinePathPrefab) as GameObject;

            if (splineObj == null)
            {
                Debug.LogError("SplinePath Prefab 인스턴스화에 실패했습니다.");
            }
            else
            {
                splineObj.transform.SetParent(_rootTransform);
            }
        }

        #endregion

        #region SplinePath Tab
        private void DrawSplinePathTab()
        {
            EditorGUILayout.LabelField("SplinePath 생성 탭", _sectionStyle);
            EditorGUILayout.BeginVertical(_boxStyle);

            // SplinePath Prefab
            _splinePathPrefab = (GameObject)EditorGUILayout.ObjectField("SplinePath Prefab", _splinePathPrefab, typeof(GameObject), false);
            EditorGUILayout.Space(5);

            // 선택된 SplinePath 정보 표시
            EditorGUILayout.LabelField("선택된 SplinePath 정보", EditorStyles.boldLabel);

            SplinePath selectedLinePath = GetSelectedSplinePath();
            if (selectedLinePath != null)
            {
                EditorGUILayout.LabelField("Name : " + selectedLinePath.name);

                // SplineContainer와 Spline의 null 체크 추가
                if (selectedLinePath.SplineContainer != null && selectedLinePath.SplineContainer.Spline != null)
                {
                    int knotCount = selectedLinePath.SplineContainer.Spline.Count;
                    EditorGUILayout.LabelField("Knot Count: " + knotCount);
                }
                else
                {
                    EditorGUILayout.LabelField("Knot Count: 0 (Spline이 초기화되지 않음)", EditorStyles.helpBox);
                }
            }
            else
            {
                EditorGUILayout.LabelField("선택된 SplinePath가 없습니다.", EditorStyles.helpBox);
            }
            EditorGUILayout.Space(5);

            // Path 설정
            EditorGUILayout.LabelField("SplinePath 옵션", EditorStyles.boldLabel);
            _length = EditorGUILayout.FloatField("Length", _length);
            EditorGUILayout.Space(5);

            // 8방향 그리드 선택
            EditorGUILayout.LabelField("Direction (8-방향)", EditorStyles.boldLabel);
            DrawDirectionGrid();
            GUILayout.Space(10);

            // 생성 버튼
            if (GUILayout.Button("SplinePath 연결", GUILayout.Height(36)))
            {
                ConnectedSplinePath();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionGrid()
        {
            float buttonSize = 40f;

            EditorGUILayout.BeginVertical();

            // 첫 번째 줄: NW, N, NE
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space((EditorGUIUtility.currentViewWidth - buttonSize * 3 - 60) / 2);
            DrawDirectionButton(EDirection.NW, "↖", buttonSize);
            DrawDirectionButton(EDirection.N, "↑", buttonSize);
            DrawDirectionButton(EDirection.NE, "↗", buttonSize);
            EditorGUILayout.EndHorizontal();

            // 두 번째 줄: W, (중앙 빈 공간), E
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space((EditorGUIUtility.currentViewWidth - buttonSize * 3 - 60) / 2);
            DrawDirectionButton(EDirection.W, "←", buttonSize);
            GUILayout.Button("", GUILayout.Width(buttonSize), GUILayout.Height(buttonSize));
            DrawDirectionButton(EDirection.E, "→", buttonSize);
            EditorGUILayout.EndHorizontal();

            // 세 번째 줄: SW, S, SE
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space((EditorGUIUtility.currentViewWidth - buttonSize * 3 - 60) / 2);
            DrawDirectionButton(EDirection.SW, "↙", buttonSize);
            DrawDirectionButton(EDirection.S, "↓", buttonSize);
            DrawDirectionButton(EDirection.SE, "↘", buttonSize);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionButton(EDirection dir, string symbol, float buttonSize)
        {
            var style = ButtonStyle.Default();
            style.BorderWhenSelected = false;

            DrawStyledButton(
                text: symbol + "\n" + dir.ToString(),
                isSelected: _direction == dir,
                onClick: () => _direction = dir,
                style: style,
                options: new[] { GUILayout.Width(buttonSize), GUILayout.Height(buttonSize) }
            );
        }

        private void DrawStyledButton(string text, bool isSelected, System.Action onClick,
            ButtonStyle style, params GUILayoutOption[] options)
        {
            Color originalBackgroundColor = GUI.backgroundColor;

            GUIStyle guiStyle = new GUIStyle(GUI.skin.button);

            // 선택 상태에 따라 스타일 적용
            if (isSelected)
            {
                GUI.backgroundColor = style.SelectedBackgroundColor;
                guiStyle.normal.textColor = style.SelectedTextColor;
                if (style.BoldWhenSelected)
                {
                    guiStyle.fontStyle = FontStyle.Bold;
                }
            }
            else
            {
                GUI.backgroundColor = style.BackgroundColor;
                guiStyle.normal.textColor = style.TextColor;
            }

            // 버튼을 그리고, 클릭되면 onClick 액션 실행
            if (GUILayout.Button(text, guiStyle, options))
            {
                onClick?.Invoke();
            }

            // 선택되었고 테두리를 그려야 한다면 테두리 추가
            if (isSelected && style.BorderWhenSelected)
            {
                Rect buttonRect = GUILayoutUtility.GetLastRect();
                Handles.color = style.BorderColor;
                Handles.DrawWireCube(buttonRect.center, buttonRect.size);
            }

            GUI.backgroundColor = originalBackgroundColor;
        }

        private void ConnectedSplinePath()
        {
            // 선택된 SplinePath 가져오기
            SplinePath selectedSplinePath = GetSelectedSplinePath();

            if (selectedSplinePath == null)
            {
                EditorUtility.DisplayDialog("오류", "SplinePath를 선택해주세요.", "확인");
                return;
            }

            var splineContainer = selectedSplinePath.gameObject.GetOrAddComponent<SplineContainer>();

            // Spline이 null이거나 Count가 0이면 초기화
            var spline = splineContainer.Spline;
            if (spline == null || spline.Count == 0)
            {
                spline = splineContainer.AddSpline();
                spline.Add(new BezierKnot(new float3(0, 0, 0)));
            }

            // Undo 기록 시작
            Undo.RecordObject(splineContainer, "Add Knot to Spline");

            // 마지막 Knot의 위치 가져오기 (로컬 좌표)
            Vector3 lastKnotPosition = Vector3.zero;
            if (spline.Count > 0)
            {
                lastKnotPosition = (Vector3)spline[spline.Count - 1].Position;
            }

            // 선택한 방향과 길이로 새로운 위치 계산
            Vector3 direction = GetDirectionVector(_direction);
            Vector3 newKnotPosition = lastKnotPosition + direction * _length;

            // 새로운 BezierKnot 생성 및 추가
            BezierKnot newKnot = new BezierKnot(newKnotPosition);
            spline.Add(newKnot);

            // 새로 추가된 Knot을 Linear 모드로 설정
            int newKnotIndex = spline.Count - 1;
            spline.SetTangentMode(newKnotIndex, TangentMode.Linear);

            // SplinePath 업데이트
            selectedSplinePath.UpdateLineRenderer();

            // 변경사항 저장
            EditorUtility.SetDirty(splineContainer);
            EditorUtility.SetDirty(selectedSplinePath);
        }

        /// <summary>
        /// 선택된 오브젝트의 SplinePath를 반환
        /// </summary>
        private SplinePath GetSelectedSplinePath()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return null;

            return go.GetComponent<SplinePath>();
        }

        private Vector3 GetDirectionVector(EDirection dir)
        {
            switch (dir)
            {
                case EDirection.N: return new Vector3(0f, 1f, 0f);
                case EDirection.NE: return new Vector3(1f, 1f, 0f).normalized;
                case EDirection.E: return new Vector3(1f, 0f, 0f);
                case EDirection.SE: return new Vector3(1f, -1f, 0f).normalized;
                case EDirection.S: return new Vector3(0f, -1f, 0f);
                case EDirection.SW: return new Vector3(-1f, -1f, 0f).normalized;
                case EDirection.W: return new Vector3(-1f, 0f, 0f);
                case EDirection.NW: return new Vector3(-1f, 1f, 0f).normalized;
            }

            return Vector3.right;
        }
        #endregion

        #region SafeZone Tab
        private void DrawSafeZoneTab()
        {
            EditorGUILayout.LabelField("SafeZone Management", _sectionStyle);
            EditorGUILayout.BeginVertical(_boxStyle);

            // SafeZone 설정
            EditorGUILayout.LabelField("SafeZone Settings", EditorStyles.boldLabel);
            _safeZonePrefab = (GameObject)EditorGUILayout.ObjectField("SafeZone Prefab", _safeZonePrefab, typeof(GameObject), false);
            _safeZoneRoot = (Transform)EditorGUILayout.ObjectField("Parent Transform", _safeZoneRoot, typeof(Transform), true);
            EditorGUILayout.Space(10);

            // SafeZone 추가 버튼
            if (GUILayout.Button("SafeZone 추가", GUILayout.Height(30)))
            {
                CreateSafeZone();
            }
            EditorGUILayout.Space(10);

            // SafeZone 자동 생성 영역
            EditorGUILayout.LabelField("SafeZone 자동 생성", EditorStyles.boldLabel);

            // 선택된 SplinePath 정보 표시
            SplinePath selectedSplinePath = GetSelectedSplinePath();
            if (selectedSplinePath != null)
            {
                EditorGUILayout.LabelField("Name : " + selectedSplinePath.name);

                // SplineContainer와 Spline의 null 체크 추가
                if (selectedSplinePath.SplineContainer != null && selectedSplinePath.SplineContainer.Spline != null)
                {
                    var spline = selectedSplinePath.SplineContainer.Spline;

                    // Knot Count 표시
                    int knotCount = spline.Count;
                    EditorGUILayout.LabelField("Knot Count: " + knotCount);

                    // 전체 길이 표시
                    float totalLength = spline.GetLength();
                    EditorGUILayout.LabelField("Total Length: " + totalLength.ToString("F2"));
                }
                else
                {
                    EditorGUILayout.LabelField("Knot Count: 0 (Spline이 초기화되지 않음)", EditorStyles.helpBox);
                }
            }
            else
            {
                EditorGUILayout.LabelField("선택된 SplinePath가 없습니다.", EditorStyles.helpBox);
            }
            EditorGUILayout.Space(5);

            // CSV 파일 할당
            _safeZoneCSVFile = EditorGUILayout.ObjectField("CSV 파일", _safeZoneCSVFile, typeof(TextAsset), false) as TextAsset;

            if (_safeZoneCSVFile != null)
            {
                EditorGUILayout.LabelField("파일명: " + _safeZoneCSVFile.name);
            }
            else
            {
                EditorGUILayout.HelpBox("CSV 파일을 선택해주세요. (distance, color 컬럼 필요)", MessageType.Info);
            }

            // SafeZone 자동 추가 버튼
            if (GUILayout.Button("SafeZone 자동 추가(CSV)", GUILayout.Height(30)))
            {
                CreateSafeZoneFromCSV();
            }
            EditorGUILayout.EndVertical();
        }

        private void CreateSafeZone()
        {
            if (_safeZoneRoot == null)
            {
                EditorUtility.DisplayDialog("오류", "SafeZone Root Transform이 설정되지 않았습니다.", "확인");
                return;
            }

            GameObject safeZoneObj = GetSafeZoneObject();
            if (safeZoneObj == null)
            {
                EditorUtility.DisplayDialog("오류", "SafeZone Prefab 인스턴스화에 실패했습니다.", "확인");
                return;
            }

            // 씬 뷰의 중앙 위치 가져오기
            Vector3 spawnPosition = GetSceneViewCenterPosition();

            safeZoneObj.transform.position = spawnPosition;
            safeZoneObj.transform.rotation = Quaternion.identity;
            safeZoneObj.name = $"SafeZone_{_safeZoneRoot.childCount + 1}";

            // Undo 시스템에 등록
            Undo.RegisterCreatedObjectUndo(safeZoneObj, "Create SafeZone");

            // 비쥬얼 업데이트
            safeZoneObj.GetComponent<SafeZone>().InitializeInEditor();
            EditorUtility.SetDirty(safeZoneObj);

            safeZoneObj.transform.SetParent(_safeZoneRoot);

            Selection.activeGameObject = safeZoneObj;

            // 씬 뷰를 생성된 오브젝트로 포커스
            FocusSceneViewOnObject(safeZoneObj);

            Debug.Log($"SafeZone 생성 완료: {safeZoneObj.name} at {spawnPosition}");
        }

        private void CreateSafeZoneFromCSV()
        {
            // 선택된 SplinePath 정보 표시
            SplinePath selectedSplinePath = GetSelectedSplinePath();

            // 유효성 검사
            if (selectedSplinePath == null)
            {
                EditorUtility.DisplayDialog("오류", "SplinePath를 선택해주세요.", "확인");
                return;
            }

            // SplineContainer 확인 및 초기화
            var splineContainer = selectedSplinePath.gameObject.GetOrAddComponent<SplineContainer>();

            if (splineContainer.Spline == null || splineContainer.Spline.Count < 2)
            {
                EditorUtility.DisplayDialog("오류", "SplinePath의 Spline이 초기화되지 않았거나 Knot이 부족합니다.\n최소 2개의 Knot이 필요합니다.", "확인");
                return;
            }

            var spline = splineContainer.Spline;
            float totalLength = spline.GetLength();

            if (totalLength <= 0f)
            {
                EditorUtility.DisplayDialog("오류", "Spline의 길이가 0입니다.", "확인");
                return;
            }

            if (_safeZoneRoot == null)
            {
                EditorUtility.DisplayDialog("오류", "SafeZone Root Transform이 설정되지 않았습니다.", "확인");
                return;
            }

            if (_safeZoneCSVFile == null)
            {
                EditorUtility.DisplayDialog("오류", "CSV 파일을 선택해주세요.", "확인");
                return;
            }

            // CSV 읽기
            var csvPath = AssetDatabase.GetAssetPath(_safeZoneCSVFile);
            var safeZoneDataList = new List<SafeZoneData>();

            try
            {
                foreach (var row in CsvUtil.Read(csvPath, skipHeader: true))
                {
                    var distance = row.Float("distance");
                    var colorStr = row.String("color");

                    // color 파싱
                    var colorKeys = ParseColorKeys(colorStr);

                    safeZoneDataList.Add(new SafeZoneData
                    {
                        Distance = distance,
                        ColorKeys = colorKeys
                    });
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("CSV 읽기 오류", $"CSV 파일을 읽는 중 오류가 발생했습니다:\n{ex.Message}", "확인");
                return;
            }

            if (safeZoneDataList.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "CSV 파일에서 읽은 SafeZone 데이터가 없습니다.", "확인");
                return;
            }

            // Undo 기록 시작
            Undo.SetCurrentGroupName("Create SafeZones from CSV");
            int undoGroup = Undo.GetCurrentGroup();

            int createdCount = 0;

            // 각 SafeZoneData에 대해 SafeZone 생성
            foreach (var safeZoneData in safeZoneDataList)
            {
                try
                {
                    // distance를 t 값(0~1)으로 변환
                    float t = Mathf.Clamp01(safeZoneData.Distance / totalLength);

                    // Spline에서 해당 위치의 월드 좌표 가져오기
                    Vector3 worldPosition = selectedSplinePath.GetPointAt(t);

                    // SplinePath의 접선 방향 가져오기
                    Vector3 tangent = selectedSplinePath.GetTangentAt(t);

                    // SafeZone 프리팹 인스턴스화
                    GameObject safeZoneObj = GetSafeZoneObject();
                    if (safeZoneObj == null)
                    {
                        Debug.LogError($"SafeZone 생성 실패: distance={safeZoneData.Distance}");
                        continue;
                    }

                    // SafeZone 컴포넌트 가져오기
                    SafeZone safeZone = safeZoneObj.GetComponent<SafeZone>();
                    if (safeZone == null)
                    {
                        Debug.LogError($"SafeZone 컴포넌트를 찾을 수 없습니다: {safeZoneObj.name}");
                        DestroyImmediate(safeZoneObj);
                        continue;
                    }

                    // 위치 설정
                    float yOffset = safeZone.Size; // SafeZone의 반지름만큼 위로
                    worldPosition += Vector3.up * yOffset;
                    safeZoneObj.transform.position = worldPosition;
                    safeZoneObj.transform.rotation = Quaternion.identity;
                    safeZoneObj.name = $"SafeZone_{_safeZoneRoot.childCount + 1}";

                    // ColorKeys를 EColorKeyType 리스트로 변환
                    List<EColorKeyType> colorKeyTypes = new List<EColorKeyType>();
                    foreach (var colorKeyStr in safeZoneData.ColorKeys)
                    {
                        if (string.IsNullOrWhiteSpace(colorKeyStr))
                            continue;

                        // EColorKeyType으로 파싱 시도
                        string trimmedKey = colorKeyStr.Trim().ToUpperInvariant();
                        if (System.Enum.TryParse<EColorKeyType>(trimmedKey, out EColorKeyType colorKeyType))
                        {
                            colorKeyTypes.Add(colorKeyType);
                        }
                        else
                        {
                            Debug.LogWarning($"알 수 없는 ColorKey: {colorKeyStr} (SafeZone: {safeZoneObj.name})");
                        }
                    }

                    // SafeZone의 ColorKeyTypes 설정 (리플렉션 사용)
                    SerializedObject serializedSafeZone = new SerializedObject(safeZone);
                    SerializedProperty colorKeyTypesProperty = serializedSafeZone.FindProperty("_colorKeyTypes");

                    if (colorKeyTypesProperty != null)
                    {
                        colorKeyTypesProperty.ClearArray();
                        for (int i = 0; i < colorKeyTypes.Count; i++)
                        {
                            colorKeyTypesProperty.InsertArrayElementAtIndex(i);

                            // enum 값을 인덱스로 변환
                            SerializedProperty elementProperty = colorKeyTypesProperty.GetArrayElementAtIndex(i);
                            EColorKeyType colorKeyType = colorKeyTypes[i];

                            // enum의 모든 값을 배열로 가져와서 인덱스 찾기
                            var enumValues = System.Enum.GetValues(typeof(EColorKeyType));
                            int enumIndex = -1;
                            for (int j = 0; j < enumValues.Length; j++)
                            {
                                if ((EColorKeyType)enumValues.GetValue(j) == colorKeyType)
                                {
                                    enumIndex = j;
                                    break;
                                }
                            }

                            if (enumIndex >= 0)
                            {
                                elementProperty.enumValueIndex = enumIndex;
                            }
                            else
                            {
                                Debug.LogError($"EColorKeyType {colorKeyType}의 인덱스를 찾을 수 없습니다.");
                            }
                        }
                        serializedSafeZone.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning("SafeZone의 _colorKeyTypes SerializedProperty를 찾을 수 없습니다.");
                    }

                    safeZone.InitializeInEditor();

                    // Undo 시스템에 등록
                    Undo.RegisterCreatedObjectUndo(safeZoneObj, "Create SafeZone from CSV");

                    // 부모 설정
                    safeZoneObj.transform.SetParent(_safeZoneRoot);

                    // 변경사항 저장
                    EditorUtility.SetDirty(safeZoneObj);
                    EditorUtility.SetDirty(safeZone);

                    // 씬 뷰 새로고침
#if UNITY_EDITOR
                    SceneView.RepaintAll();
#endif

                    createdCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"SafeZone 생성 중 오류 발생 (distance={safeZoneData.Distance}): {ex.Message}");
                }
            }

            // Undo 그룹 마무리
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[SplinePathCreator] CSV에서 {createdCount}개의 SafeZone을 생성했습니다.");
        }

        private GameObject GetSafeZoneObject()
        {
            if (_safeZonePrefab == null)
            {
                EditorUtility.DisplayDialog("오류", $"SafeZone Prefab이 할당되어 있지 않습니다.", "확인");
                return null;
            }

            GameObject safeZoneObj = PrefabUtility.InstantiatePrefab(_safeZonePrefab) as GameObject;
            if (safeZoneObj == null)
            {
                Debug.LogError("SafeZone Prefab 인스턴스화에 실패했습니다.");
                return null;
            }

            // 프리팹 연결 해제하여 일반 GameObject로 변환
#if UNITY_EDITOR
            PrefabUtility.UnpackPrefabInstance(safeZoneObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
#endif

            return safeZoneObj;
        }

        /// <summary>
        /// 현재 활성화된 씬 뷰의 중앙 위치를 반환
        /// </summary>
        private Vector3 GetSceneViewCenterPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null)
            {
                // pivot은 씬 뷰의 중심점을 나타냄
                return sceneView.pivot;
            }

            // 씬 뷰가 없으면 기본값 반환
            return Vector3.zero;
        }

        /// <summary>
        /// 씬 뷰를 특정 오브젝트로 포커스
        /// </summary>
        private void FocusSceneViewOnObject(GameObject obj)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && obj != null)
            {
                // 씬 뷰를 오브젝트 위치로 이동
                sceneView.LookAt(obj.transform.position);

                // 씬 뷰 새로고침
                sceneView.Repaint();
            }
        }

        /// <summary>
        /// ColorKeys 문자열을 파싱하여 리스트로 반환 (예: "F" -> ["F"], "F,D" -> ["F", "D"])
        /// </summary>
        private List<string> ParseColorKeys(string colorStr)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrWhiteSpace(colorStr))
                return result;

            // 쉼표로 분리
            string[] parts = colorStr.Split(',');
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        #endregion

        #region OtherObjects Tab
        private void DrawOtherObjectsTab()
        {
            EditorGUILayout.LabelField("Other Objects Management", _sectionStyle);
            EditorGUILayout.BeginVertical(_boxStyle);

            // EndPoint 설정
            EditorGUILayout.LabelField("EndPoint Settings", EditorStyles.boldLabel);
            _endPointPrefab = (GameObject)EditorGUILayout.ObjectField("EndPoint Prefab", _endPointPrefab, typeof(GameObject), false);

            if (_rootTransform == null)
            {
                EditorGUILayout.HelpBox("Root Transform이 설정되지 않았습니다. Map Settings 탭에서 Root Transform을 설정하세요.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Parent: " + _rootTransform.name);
            }

            EditorGUILayout.Space(10);

            // EndPoint 생성 버튼
            if (GUILayout.Button("EndPoint 생성", GUILayout.Height(30)))
            {
                CreateEndPoint();
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateEndPoint()
        {
            if (_endPointPrefab == null)
            {
                EditorUtility.DisplayDialog("오류", $"EndPoint Prefab이 할당되어 있지 않습니다.", "확인");
                return;
            }

            if (_rootTransform == null)
            {
                EditorUtility.DisplayDialog("오류", "Root Transform이 설정되지 않았습니다.\nMap Settings 탭에서 Root Transform을 설정해주세요.", "확인");
                return;
            }

            GameObject endPointObj = PrefabUtility.InstantiatePrefab(_endPointPrefab) as GameObject;
            if (endPointObj == null)
            {
                EditorUtility.DisplayDialog("오류", "EndPoint Prefab 인스턴스화에 실패했습니다.", "확인");
                return;
            }

            // 씬 뷰의 중앙 위치 가져오기
            Vector3 spawnPosition = GetSceneViewCenterPosition();

            endPointObj.transform.position = spawnPosition;
            endPointObj.transform.rotation = Quaternion.identity;

            // Undo 시스템에 등록
            Undo.RegisterCreatedObjectUndo(endPointObj, "Create EndPoint");

            // 부모 설정
            endPointObj.transform.SetParent(_rootTransform);

            // 변경사항 저장
            EditorUtility.SetDirty(endPointObj);

            Selection.activeGameObject = endPointObj;

            // 씬 뷰를 생성된 오브젝트로 포커스
            FocusSceneViewOnObject(endPointObj);

            Debug.Log($"EndPoint 생성 완료: {endPointObj.name} at {spawnPosition}");
        }
        #endregion
    }
}
