#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

namespace RollingEgg.EditorTools
{
    public class LocalizationImporterWindow : EditorWindow
    {
        // UI 상태 변수
        private TextAsset csvFile;
        private string targetCollectionName = "StringTable"; // 기본 테이블 이름
        private string tablesBasePath = "Assets/03. Prefabs/UI/Localization/Tables"; // 기본 저장 경로
        
        // CSV 파싱 옵션
        private int headerRowIndex = 0; // 헤더가 있는 줄 (0부터 시작)
        private char separator = ',';   // 구분자 (CSV는 콤마)

        // 파싱된 데이터 미리보기
        private List<string[]> parsedData = new List<string[]>();
        private string[] headers;
        private Dictionary<int, string> columnMapping = new Dictionary<int, string>(); // 컬럼 인덱스 -> 로캘 코드
        private int keyColumnIndex = 1; // 키(Key)가 있는 컬럼 인덱스
        private int tableColumnIndex = 0; // 테이블 이름이 있는 컬럼 인덱스 (없으면 -1)

        // 스크롤 위치
        private Vector2 scrollPos;

        [MenuItem("Tools/RollingEgg/Localization Importer (CSV)")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationImporterWindow>("Loc Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("CSV Localization Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 1. 파일 선택
            GUILayout.Label("1. CSV 파일 선택", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck() && csvFile != null)
            {
                ParseCSV();
            }

            if (csvFile == null) return;

            EditorGUILayout.Space();

            // 2. 파싱 옵션
            GUILayout.Label("2. 파싱 옵션", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            headerRowIndex = EditorGUILayout.IntField("Header Row Index", headerRowIndex);
            keyColumnIndex = EditorGUILayout.IntField("Key Column Index", keyColumnIndex);
            tableColumnIndex = EditorGUILayout.IntField("Table Name Column Index", tableColumnIndex);
            
            if (EditorGUI.EndChangeCheck())
            {
                ParseCSV();
            }
            
            EditorGUILayout.HelpBox("Table Name Column이 -1이면 아래 설정된 'Default Table Name'을 사용합니다.", MessageType.Info);
            if (tableColumnIndex == -1)
            {
                targetCollectionName = EditorGUILayout.TextField("Default Table Name", targetCollectionName);
            }
            
            tablesBasePath = EditorGUILayout.TextField("Save Path", tablesBasePath);

            EditorGUILayout.Space();

            // 3. 컬럼 매핑 (헤더가 있을 때만 표시)
            if (headers != null && headers.Length > 0)
            {
                GUILayout.Label("3. 언어(Locale) 매핑", EditorStyles.boldLabel);
                
                for (int i = 0; i < headers.Length; i++)
                {
                    if (i == keyColumnIndex || i == tableColumnIndex) continue;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Col {i}: {headers[i]}", GUILayout.Width(150));
                    
                    string currentLocale = columnMapping.ContainsKey(i) ? columnMapping[i] : "";
                    string newLocale = EditorGUILayout.TextField(currentLocale);
                    
                    if (!string.IsNullOrEmpty(newLocale))
                    {
                        columnMapping[i] = newLocale;
                    }
                    else if (columnMapping.ContainsKey(i))
                    {
                        columnMapping.Remove(i);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.HelpBox("매핑할 로캘 코드(예: ko-KR, en, ja-JP)를 입력하세요. 비워두면 무시합니다.", MessageType.None);
            }

            EditorGUILayout.Space();

            // 4. 실행 버튼 (미리보기 확인 후 실행)
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Import to Localization Tables (Apply)", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("확인", "정말로 임포트 하시겠습니까?\n기존 데이터가 덮어씌워질 수 있습니다.", "예 (Yes)", "아니오 (No)"))
                {
                    ImportData();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            GUILayout.Label($"미리보기 (총 {parsedData.Count}개 행)", EditorStyles.boldLabel);
            
            // 미리보기 스크롤 뷰
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            foreach (var row in parsedData.Take(20)) // 상위 20개만 표시
            {
                EditorGUILayout.BeginHorizontal("box");
                foreach (var cell in row)
                {
                    GUILayout.Label(cell, GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();
            }
            if (parsedData.Count > 20) GUILayout.Label("... (더 많은 데이터 생략됨)");
            EditorGUILayout.EndScrollView();
        }

        private void ParseCSV()
        {
            parsedData.Clear();
            columnMapping.Clear();
            headers = null;

            if (csvFile == null) return;

            string text = csvFile.text;
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= headerRowIndex) return;

            // 헤더 파싱
            headers = ParseCsvLine(lines[headerRowIndex]);

            // 자동 매핑 시도 (헤더 이름이 로캘 코드와 같으면 자동 매핑)
            for (int i = 0; i < headers.Length; i++)
            {
                if (i == keyColumnIndex || i == tableColumnIndex) continue;
                
                // 간단한 로캘 코드 추측 (실제로는 더 정교하게 할 수 있음)
                string header = headers[i].Trim();
                if (header.Contains("ko") || header.Contains("KR")) columnMapping[i] = "ko-KR";
                else if (header.Equals("en", StringComparison.OrdinalIgnoreCase)) columnMapping[i] = "en";
                else if (header.Contains("jp") || header.Contains("Japan")) columnMapping[i] = "ja-JP";
                else if (header.Contains("zh") && header.Contains("Hant")) columnMapping[i] = "zh-Hant";
                else if (header.Contains("zh") && header.Contains("Hans")) columnMapping[i] = "zh-Hans";
            }

            // 데이터 파싱
            for (int i = headerRowIndex + 1; i < lines.Length; i++)
            {
                parsedData.Add(ParseCsvLine(lines[i]));
            }
        }

        private void ImportData()
        {
            if (parsedData.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "데이터가 없습니다.", "OK");
                return;
            }

            if (columnMapping.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "매핑된 언어(Locale)가 하나도 없습니다.", "OK");
                return;
            }

            int successCount = 0;
          

            try
            {
                // 테이블별 데이터 정리
                // Dictionary<TableName, Dictionary<Key, Dictionary<Locale, Value>>>
                var tableData = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                foreach (var row in parsedData)
                {
                    if (row.Length <= keyColumnIndex) continue;

                    string key = row[keyColumnIndex].Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    string tableName = targetCollectionName;
                    if (tableColumnIndex >= 0 && row.Length > tableColumnIndex)
                    {
                        tableName = row[tableColumnIndex].Trim();
                    }

                    if (string.IsNullOrEmpty(tableName)) continue;

                    if (!tableData.ContainsKey(tableName))
                        tableData[tableName] = new Dictionary<string, Dictionary<string, string>>();

                    if (!tableData[tableName].ContainsKey(key))
                        tableData[tableName][key] = new Dictionary<string, string>();

                    // 각 로캘별 값 저장
                    foreach (var kvp in columnMapping)
                    {
                        int colIdx = kvp.Key;
                        string localeCode = kvp.Value;

                        if (colIdx < row.Length)
                        {
                            tableData[tableName][key][localeCode] = row[colIdx];
                        }
                    }
                }

                // 실제 Unity Localization Table에 쓰기
                foreach (var tableKvp in tableData)
                {
                    string tableName = tableKvp.Key;
                    var entries = tableKvp.Value;
                    
                    // 사용된 모든 로캘 코드 수집
                    var usedLocales = new HashSet<string>();
                    foreach(var entry in entries.Values)
                        foreach(var loc in entry.Keys)
                            usedLocales.Add(loc);

                    ImportToTable(tableName, entries, usedLocales.ToList());
                    successCount += entries.Count;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Success", $"임포트 완료!\n총 {successCount}개 엔트리 처리됨.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocImporter] Error: {e}");
                EditorUtility.DisplayDialog("Error", $"임포트 중 오류 발생:\n{e.Message}", "OK");
            }
        }

        private void ImportToTable(string tableName, Dictionary<string, Dictionary<string, string>> entries, List<string> localeCodes)
        {
            // StringTableCollection 찾기 또는 생성
            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                string fullPath = Path.Combine(tablesBasePath, tableName);
                if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                
                collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, fullPath);
            }

            // 로캘별 테이블 준비
            var localeTables = new Dictionary<string, StringTable>();
            var dirtyTables = new HashSet<StringTable>();
            bool sharedDataDirty = false;

            foreach (var code in localeCodes)
            {
                var locale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
                if (locale == null)
                {
                    Debug.LogWarning($"로캘을 찾을 수 없음: {code} (Project Settings > Localization에서 추가했는지 확인하세요)");
                    continue;
                }

                var table = collection.GetTable(locale.Identifier) as StringTable;
                if (table == null)
                {
                    collection.AddNewTable(locale.Identifier);
                    table = collection.GetTable(locale.Identifier) as StringTable;
                }
                localeTables[code] = table;
            }

            // 데이터 입력
            foreach (var entryKvp in entries)
            {
                string key = entryKvp.Key;
                var translations = entryKvp.Value;

                // Shared Data (Key) 생성
                var sharedData = collection.SharedData;
                long id = sharedData.GetId(key);
                if (id == 0)
                {
                    id = sharedData.AddKey(key).Id;
                    sharedDataDirty = true;
                }

                // 각 언어별 값 입력
                foreach (var transKvp in translations)
                {
                    string code = transKvp.Key;
                    string value = transKvp.Value;

                    if (!localeTables.TryGetValue(code, out var table))
                        continue;

                    var entry = table.GetEntry(id);
                    if (entry == null)
                    {
                        table.AddEntry(id, value);
                        dirtyTables.Add(table);
                        continue;
                    }

                    if (string.Equals(entry.Value, value, StringComparison.Ordinal))
                        continue;

                    entry.Value = value;
                    dirtyTables.Add(table);
                }
            }

            if (sharedDataDirty)
                EditorUtility.SetDirty(collection.SharedData);

            foreach (var table in dirtyTables)
            {
                if (table != null)
                    EditorUtility.SetDirty(table);
            }
        }

        // CSV 파싱 헬퍼 (따옴표 처리 포함)
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"'); i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == separator && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
#endif

