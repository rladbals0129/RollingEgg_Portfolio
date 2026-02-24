using RollingEgg.Data;
using RollingEgg.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RollingEgg
{
    public static class StageTableBuilder
    {
        private const string CsvPath = "Assets/10. Datas/StageTable.csv";
        private const string OutputPath = "Assets/06. ScriptableObject/Data";

        private const string AssetFileName = "StageTableSO.asset";

        // Stage 테이블 컬럼명
        private const string COL_STAGE_ID = "id";
        private const string COL_STAGE_CHAPTERID = "chapterId";
        private const string COL_STAGE_STAGENUMBER = "stageNo";
        private const string COL_STAGE_KEYCOUNT = "keyCount";
        private const string COL_STAGE_KEYS = "keys";
        private const string COL_STAGE_OPENSTAGEID = "open_stageID";
        private const string COL_STAGE_LEVEL = "level";
        private const string COL_STAGE_SPEED = "speed";
        private const string COL_STAGE_HP = "hp";
        private const string COL_STAGE_REDUCEHP = "reduceHP";
        private const string COL_STAGE_PERPECTHP = "perfectHP";
        private const string COL_STAGE_GREATHP = "greatHP";
        private const string COL_STAGE_GOODHP = "goodHP";
        private const string COL_STAGE_BADHP = "badHP";
        private const string COL_STAGE_MISSHP = "missHP";
        private const string COL_STAGE_COLORCOOLTIME = "changeColorCooltime";

        [MenuItem("Tools/RollingEgg/TableBuilder/StageTable")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputPath);

            BuildStage();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageTableBuilder] 빌드 완료");
        }

        private static void BuildStage()
        {
            var rows = new List<StageTableSO.StageRow>();

            // 기존 ScriptableObject 로드 (있는 경우)
            string assetPath = Path.Combine(OutputPath, AssetFileName).Replace('\\', '/');
            StageTableSO existingStageTable = AssetDatabase.LoadAssetAtPath<StageTableSO>(assetPath);

            // 기존 데이터를 id로 매핑 (background, mapPrefab 유지용)
            Dictionary<int, (GameObject background, GameObject mapPrefab)> existingData = new Dictionary<int, (GameObject, GameObject)>();
            if (existingStageTable != null && existingStageTable.Rows != null)
            {
                foreach (var existingRow in existingStageTable.Rows)
                {
                    if (existingRow != null)
                    {
                        existingData[existingRow.id] = (existingRow.background, existingRow.mapPrefab);
                    }
                }
            }

            foreach (var row in CsvUtil.Read(CsvPath, skipHeader: true))
            {
                try
                {
                    int stageId = row.Int(COL_STAGE_ID);

                    var stageRow = new StageTableSO.StageRow
                    {
                        id = stageId,
                        chapterId = row.Int(COL_STAGE_CHAPTERID),
                        stageNumber = row.Int(COL_STAGE_STAGENUMBER),
                        keyCount = row.Int(COL_STAGE_KEYCOUNT),
                        openStageId = row.Int(COL_STAGE_OPENSTAGEID),
                        level = row.Int(COL_STAGE_LEVEL),
                        speed = row.Float(COL_STAGE_SPEED),
                        hp = row.Float(COL_STAGE_HP),
                        reduceHP = row.Float(COL_STAGE_REDUCEHP),
                        perfectHP = row.Float(COL_STAGE_PERPECTHP),
                        greatHP = row.Float(COL_STAGE_GREATHP),
                        goodHP = row.Float(COL_STAGE_GOODHP),
                        badHP = row.Float(COL_STAGE_BADHP),
                        missHP = row.Float(COL_STAGE_MISSHP),
                        changeColorCooltime = row.Float(COL_STAGE_COLORCOOLTIME),
                    };

                    // keys 파싱 (쉼표로 구분된 문자열을 List<string>으로 변환)
                    string keysStr = row.String(COL_STAGE_KEYS);
                    if (!string.IsNullOrWhiteSpace(keysStr))
                    {
                        stageRow.keys = keysStr.Split(',')
                            .Select(key => key.Trim().ToUpperInvariant())
                            .Where(key => !string.IsNullOrEmpty(key))
                            .ToList();

                        // keyCount와 keys 개수가 다르면 keyCount를 keys 개수로 업데이트
                        if (stageRow.keys.Count > 0)
                        {
                            stageRow.keyCount = stageRow.keys.Count;
                        }
                    }
                    else
                    {
                        stageRow.keys = new List<string>();
                        stageRow.keyCount = 0;
                    }

                    // 기존 데이터에서 background와 mapPrefab 가져오기
                    if (existingData.TryGetValue(stageId, out var existingAssets))
                    {
                        stageRow.background = existingAssets.background;
                        stageRow.mapPrefab = existingAssets.mapPrefab;
                    }
                    else
                    {
                        // 기존 데이터가 없으면 null로 설정
                        stageRow.background = null;
                        stageRow.mapPrefab = null;
                    }

                    rows.Add(stageRow);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[StageTableBuilder] CSV 행 파싱 중 오류 발생: {ex.Message}");
                }
            }

            if (rows.Count == 0)
            {
                Debug.LogWarning("[StageTableBuilder] 생성된 StageRow가 없습니다.");
                return;
            }

            SaveStageTable(rows, existingStageTable);
        }
        private static void SaveStageTable(List<StageTableSO.StageRow> rows, StageTableSO existingStageTable)
        {
            string assetPath = Path.Combine(OutputPath, AssetFileName).Replace('\\', '/');

            // ScriptableObject 생성 또는 업데이트
            StageTableSO stageTable = existingStageTable;
            if (stageTable == null)
            {
                // 새로 생성
                stageTable = ScriptableObject.CreateInstance<StageTableSO>();
                AssetDatabase.CreateAsset(stageTable, assetPath);
            }

            // 리플렉션을 사용하여 private 필드에 접근
            var rowsField = typeof(StageTableSO).GetField("rows",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (rowsField != null)
            {
                rowsField.SetValue(stageTable, rows);
            }
            else
            {
                Debug.LogError("[StageTableBuilder] StageTableSO의 rows 필드를 찾을 수 없습니다.");
                return;
            }

            EditorUtility.SetDirty(stageTable);
            Debug.Log($"[StageTableBuilder] {rows.Count}개의 StageRow를 생성했습니다. 저장 경로: {assetPath}");
        }
    }
}
