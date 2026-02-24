using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RollingEgg.Util
{
    /// <summary>
    /// CSV 파일을 읽고 파싱하는 유틸리티 클래스
    /// </summary>
    public static class CsvUtil
    {
        /// <summary>
        /// CSV 파일을 읽어서 행들의 열거형을 반환합니다.
        /// </summary>
        /// <param name="csvPath">CSV 파일 경로</param>
        /// <param name="skipHeader">헤더 행을 건너뛸지 여부</param>
        /// <returns>CSV 행들의 열거형</returns>
        public static IEnumerable<CsvRow> Read(string csvPath, bool skipHeader = false)
        {
            if (string.IsNullOrEmpty(csvPath))
            {
                Debug.LogError("[CsvUtil] CSV 경로가 비어있습니다.");
                yield break;
            }

            if (!File.Exists(csvPath))
            {
                Debug.LogError($"[CsvUtil] CSV 파일을 찾을 수 없습니다: {csvPath}");
                yield break;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CsvUtil] CSV 파일 읽기 실패: {ex.Message}");
                yield break;
            }

            if (lines.Length == 0)
            {
                Debug.LogWarning("[CsvUtil] CSV 파일이 비어있습니다.");
                yield break;
            }

            // 헤더 파싱
            string[] headers = ParseCsvLine(lines[0]);
            Dictionary<string, int> headerIndexMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    headerIndexMap[header] = i;
                }
            }

            // 데이터 행 처리
            int startIndex = skipHeader ? 1 : 0;
            for (int i = startIndex; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] values = ParseCsvLine(lines[i]);
                yield return new CsvRow(headerIndexMap, values);
            }
        }

        /// <summary>
        /// CSV 라인을 파싱하여 필드 배열로 반환합니다. (따옴표 처리 포함)
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // 이스케이프된 따옴표 처리 ("")
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 다음 따옴표 건너뛰기
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // 마지막 필드 추가
            result.Add(current.ToString());

            return result.ToArray();
        }
    }

    /// <summary>
    /// CSV의 한 행을 나타내는 클래스
    /// </summary>
    public class CsvRow
    {
        private readonly Dictionary<string, int> _headerIndexMap;
        private readonly string[] _values;

        public CsvRow(Dictionary<string, int> headerIndexMap, string[] values)
        {
            _headerIndexMap = headerIndexMap ?? new Dictionary<string, int>();
            _values = values ?? new string[0];
        }

        /// <summary>
        /// 컬럼 이름으로 문자열 값을 가져옵니다.
        /// </summary>
        public string String(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return string.Empty;

            if (_headerIndexMap.TryGetValue(columnName, out int index))
            {
                if (index >= 0 && index < _values.Length)
                {
                    return _values[index] ?? string.Empty;
                }
            }

            Debug.LogWarning($"[CsvRow] 컬럼 '{columnName}'을 찾을 수 없습니다.");
            return string.Empty;
        }

        /// <summary>
        /// 컬럼 이름으로 float 값을 가져옵니다.
        /// </summary>
        public float Float(string columnName)
        {
            string value = String(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return 0f;

            if (float.TryParse(value, out float result))
            {
                return result;
            }

            Debug.LogWarning($"[CsvRow] 컬럼 '{columnName}'의 값을 float로 변환할 수 없습니다: '{value}'");
            return 0f;
        }

        /// <summary>
        /// 컬럼 이름으로 int 값을 가져옵니다.
        /// </summary>
        public int Int(string columnName)
        {
            string value = String(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (int.TryParse(value, out int result))
            {
                return result;
            }

            Debug.LogWarning($"[CsvRow] 컬럼 '{columnName}'의 값을 int로 변환할 수 없습니다: '{value}'");
            return 0;
        }

        /// <summary>
        /// 컬럼 이름으로 bool 값을 가져옵니다.
        /// </summary>
        public bool Bool(string columnName)
        {
            string value = String(columnName);

            if (string.IsNullOrWhiteSpace(value))
                return false;

            // "true", "True", "1", "yes", "Yes" 등을 true로 처리
            value = value.Trim().ToLowerInvariant();
            return value == "true" || value == "1" || value == "yes";
        }
    }
}