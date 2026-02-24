using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using RollingEgg.Data;
using RollingEgg.Util;
using UnityEngine;

namespace RollingEgg.Core
{
    /// <summary>
    /// StageTableSO를 기반으로 스테이지 데이터와 진행도를 관리한다.
    /// </summary>
    public class StageService : IStageService
    {
        private const string SaveFileName = "StageProgress.json";

        private readonly Dictionary<int, StageTableSO.StageRow> _stageById = new();
        private readonly Dictionary<int, List<StageTableSO.StageRow>> _stagesByChapter = new();
        private readonly Dictionary<int, StageProgressEntry> _progressByStage = new();

        private StageTableSO _stageTable;
        private IResourceService _resourceService;
        private string _savePath;
        private bool _isDirty;

        private int _lastSelectedChapterId;
        private int _lastSelectedStageId;

        public async UniTask InitializeAsync()
        {
            _resourceService = ServiceLocator.Get<IResourceService>();
            if (_resourceService == null)
            {
                Debug.LogError("[StageService] ResourceService가 등록되지 않았습니다.");
                return;
            }

            await LoadStageTableAsync();

            _savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            await LoadDataAsync();
        }

        public IReadOnlyList<StageTableSO.StageRow> GetStagesByChapter(int chapterId)
        {
            if (chapterId <= 0 || !_stagesByChapter.TryGetValue(chapterId, out var list))
                return Array.Empty<StageTableSO.StageRow>();

            return list;
        }

        public bool TryGetStage(int stageId, out StageTableSO.StageRow row)
        {
            return _stageById.TryGetValue(stageId, out row);
        }

        public StageTableSO.StageRow GetStage(int stageId)
        {
            _stageById.TryGetValue(stageId, out var row);
            return row;
        }

        public StageProgressData GetProgress(int stageId)
        {
            if (_progressByStage.TryGetValue(stageId, out var entry))
            {
                return new StageProgressData(entry.cleared, entry.bestRank, entry.bestScore, entry.lastPlayedTicks);
            }

            return StageProgressData.Default;
        }

        public bool IsStageUnlocked(int stageId)
        {
            if (!_stageById.TryGetValue(stageId, out var row))
                return false;

            if (row.openStageId <= 0)
                return true;

            var prerequisite = GetProgress(row.openStageId);
            return prerequisite.IsCleared;
        }

        public void UpdateStageResult(int stageId, int score, EClearRank rank)
        {
            if (!_stageById.ContainsKey(stageId))
                return;

            var entry = GetOrCreateEntry(stageId);
            entry.cleared = true;
            entry.bestScore = Mathf.Max(entry.bestScore, score);

            if (IsBetterRank(rank, entry.bestRank))
            {
                entry.bestRank = rank;
            }

            entry.lastPlayedTicks = DateTime.UtcNow.Ticks;
            _progressByStage[stageId] = entry;
            _isDirty = true;
        }

        public int GetLastSelectedChapter()
        {
            if (_lastSelectedChapterId > 0)
                return _lastSelectedChapterId;

            if (_stagesByChapter.Count == 0)
                return 0;

            return _stagesByChapter.Keys.Min();
        }

        public int GetLastSelectedStage()
        {
            if (_lastSelectedStageId > 0)
                return _lastSelectedStageId;

            var chapterId = GetLastSelectedChapter();
            var stages = GetStagesByChapter(chapterId);
            return stages.Count > 0 ? stages[0].id : 0;
        }

        public void SetLastSelection(int chapterId, int stageId)
        {
            if (chapterId <= 0 || stageId <= 0)
                return;

            _lastSelectedChapterId = chapterId;
            _lastSelectedStageId = stageId;
            _isDirty = true;
        }

        public async UniTask SaveDataAsync()
        {
            try
            {
                if (!_isDirty)
                    return;

                var data = new StageSaveData
                {
                    lastSelectedChapterId = _lastSelectedChapterId,
                    lastSelectedStageId = _lastSelectedStageId,
                    saveTimeTicks = DateTime.UtcNow.Ticks,
                    entries = _progressByStage.Values
                        .Select(e => new StageProgressEntry
                        {
                            stageId = e.stageId,
                            cleared = e.cleared,
                            bestRank = e.bestRank,
                            bestScore = e.bestScore,
                            lastPlayedTicks = e.lastPlayedTicks
                        })
                        .ToArray()
                };

                var json = JsonUtility.ToJson(data, true);
                await File.WriteAllTextAsync(_savePath, json);
                _isDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StageService] SaveDataAsync 실패: {e.Message}");
            }
        }

        public async UniTask LoadDataAsync()
        {
            try
            {
                if (!File.Exists(_savePath))
                {
                    _isDirty = true;
                    return;
                }

                var json = await File.ReadAllTextAsync(_savePath);
                if (string.IsNullOrEmpty(json))
                    return;

                var data = JsonUtility.FromJson<StageSaveData>(json);
                if (data?.entries != null)
                {
                    _progressByStage.Clear();
                    foreach (var entry in data.entries)
                    {
                        if (!_stageById.ContainsKey(entry.stageId))
                            continue;

                        _progressByStage[entry.stageId] = new StageProgressEntry
                        {
                            stageId = entry.stageId,
                            cleared = entry.cleared,
                            bestRank = entry.bestRank,
                            bestScore = entry.bestScore,
                            lastPlayedTicks = entry.lastPlayedTicks
                        };
                    }
                }

                _lastSelectedChapterId = data?.lastSelectedChapterId ?? 0;
                _lastSelectedStageId = data?.lastSelectedStageId ?? 0;
                _isDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StageService] LoadDataAsync 실패: {e.Message}");
                _isDirty = true;
            }
        }

        private async UniTask LoadStageTableAsync()
        {
            try
            {
                _stageTable = await _resourceService.LoadAssetAsync<StageTableSO>("StageTableSO");
                if (_stageTable == null)
                {
                    Debug.LogError("[StageService] StageTableSO를 로드하지 못했습니다.");
                    return;
                }

                BuildCaches();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StageService] StageTableSO 로드 실패: {e.Message}");
            }
        }

        private void BuildCaches()
        {
            _stageById.Clear();
            _stagesByChapter.Clear();

            if (_stageTable?.Rows == null)
                return;

            foreach (var row in _stageTable.Rows)
            {
                if (row == null)
                    continue;

                _stageById[row.id] = row;

                if (!_stagesByChapter.TryGetValue(row.chapterId, out var list))
                {
                    list = new List<StageTableSO.StageRow>();
                    _stagesByChapter[row.chapterId] = list;
                }

                list.Add(row);
            }

            foreach (var kvp in _stagesByChapter)
            {
                kvp.Value.Sort((a, b) => a.id.CompareTo(b.id));
            }
        }

        private StageProgressEntry GetOrCreateEntry(int stageId)
        {
            if (_progressByStage.TryGetValue(stageId, out var entry))
                return entry;

            entry = new StageProgressEntry
            {
                stageId = stageId,
                cleared = false,
                bestRank = EClearRank.F,
                bestScore = 0,
                lastPlayedTicks = 0
            };

            _progressByStage[stageId] = entry;
            return entry;
        }

        private static bool IsBetterRank(EClearRank newRank, EClearRank current)
        {
            return (int)newRank > (int)current;
        }

        [Serializable]
        private class StageProgressEntry
        {
            public int stageId;
            public bool cleared;
            public EClearRank bestRank = EClearRank.F;
            public int bestScore;
            public long lastPlayedTicks;
        }

        [Serializable]
        private class StageSaveData
        {
            public StageProgressEntry[] entries;
            public int lastSelectedChapterId;
            public int lastSelectedStageId;
            public long saveTimeTicks;
        }
    }
}


