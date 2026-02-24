using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.Data;
using RollingEgg.UI;
using RollingEgg.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class UI_Stage : UI_Popup
    {
        [Header("Stage List")]
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private UI_StageItem _stageItemPrefab;
        [SerializeField] private GameObject _emptyStateObject;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Info")]
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private CanvasGroup _contentCanvasGroup;

        private readonly List<UI_StageItem> _spawnedItems = new();

        private IStageService _stageService;
        private IRunningService _runningService;
        private IAudioService _audioService;

        private bool _isRunningStartInProgress;

        private int _currentChapterId;
        private int _currentEggId;
        private string _currentEggType;

        public override async UniTask InitializeAsync()
        {
            _stageService = ServiceLocator.Get<IStageService>();
            _runningService = ServiceLocator.Get<IRunningService>();
            _audioService = ServiceLocator.Get<IAudioService>();

            await UniTask.Yield();
        }

        public override void OnShow()
        {
            // 챕터 정보가 설정되기 전에는 렌더하지 않는다 (빈 리스트 깜빡임 방지)
            if (_currentChapterId <= 0)
            {
                SetContentVisible(false);
                return;
            }

            SetContentVisible(false);
            RenderStageList();
            SetContentVisible(true);
        }

        public void OpenChapter(int chapterId, int eggId, string eggType)
        {
            _currentChapterId = chapterId;
            _currentEggId = eggId;
            _currentEggType = string.IsNullOrEmpty(eggType) ? "blue" : eggType;

            if (_progressText != null)
                _progressText.text = $"{eggType}_progress : {0}/{10}";

            SetContentVisible(false);
            UpdateProgressText();
            RenderStageList();
            SetContentVisible(true);
        }

        private void UpdateProgressText()
        {
            if (_progressText == null || _stageService == null)
                return;

            var stages = _stageService.GetStagesByChapter(_currentChapterId);
            int totalStages = stages?.Count ?? 0;
            int clearedStages = 0;

            if (totalStages > 0)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    if (_stageService.GetProgress(stages[i].id).IsCleared)
                        clearedStages++;
                }
            }

            string displayType = string.IsNullOrEmpty(_currentEggType) ? "blue" : _currentEggType;
            _progressText.text = $"{displayType}_Progress : {clearedStages}/{totalStages}";
        }

        private void RenderStageList()
        {
            if (_currentChapterId <= 0)
            {
                SetContentVisible(false);
                return;
            }

            if (_stageService == null || _stageItemPrefab == null || _contentRoot == null)
            {
                Debug.LogWarning("[UI_Stage] StageService 또는 리스트 프리팹이 설정되지 않았습니다.");
                return;
            }

            var stages = _stageService.GetStagesByChapter(_currentChapterId);
            bool hasStage = stages != null && stages.Count > 0;

            if (_emptyStateObject != null)
                _emptyStateObject.SetActive(!hasStage);

            if (!hasStage)
            {
                HideAllItems();
                return;
            }

            EnsureItemCount(stages.Count);

            for (int i = 0; i < stages.Count; i++)
            {
                var row = stages[i];
                var item = _spawnedItems[i];
                bool unlocked = _stageService.IsStageUnlocked(row.id);
                var progress = _stageService.GetProgress(row.id);

                item.gameObject.SetActive(true);
                item.SetData(row, progress, unlocked, OnSelectStage);
            }

            for (int i = stages.Count; i < _spawnedItems.Count; i++)
            {
                _spawnedItems[i].gameObject.SetActive(false);
            }

            // 스크롤 위치 초기화
            if (_scrollRect != null)
            {
                _scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        private void EnsureItemCount(int required)
        {
            while (_spawnedItems.Count < required)
            {
                var instance = Instantiate(_stageItemPrefab, _contentRoot);
                _spawnedItems.Add(instance);
            }
        }

        private void HideAllItems()
        {
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                _spawnedItems[i].gameObject.SetActive(false);
            }
        }

        private void OnSelectStage(StageTableSO.StageRow row)
        {
            if (row == null)
                return;

            if (_stageService != null && !_stageService.IsStageUnlocked(row.id))
            {
                Debug.LogWarning($"[UI_Stage] 잠겨있는 스테이지입니다. id={row.id}, openStageId={row.openStageId}");
                return;
            }

            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            StartStageAsync(row).Forget();
        }

        private async UniTaskVoid StartStageAsync(StageTableSO.StageRow row)
        {
            if (_isRunningStartInProgress)
                return;

            if (_runningService == null)
            {
                Debug.LogError("[UI_Stage] RunningService가 등록되지 않았습니다.");
                return;
            }

            _stageService?.SetLastSelection(_currentChapterId, row.id);
            _runningService.SetRunningEgg(_currentEggId, _currentEggType);

            _isRunningStartInProgress = true;
            try
            {
                // 1. FadeOut 시작하면서 인스턴스 준비 및 UI 전환
                await UIManager.Instance.RunWithFadeAsync(async () =>
                {
                    // FadeOut 완료 후 실행되는 부분
                    // 맵, 플레이어 등 인스턴스 준비
                    var prepareResult = await _runningService.PrepareRunningInstancesAsync(_currentEggId, row.id);

                    if (!prepareResult)
                    {
                        Debug.LogError("[UI_Stage] 인스턴스 준비에 실패했습니다.");
                        return;
                    }

                    // UI 전환: Stage 닫기
                    UIManager.Instance.CloseCurrentPopup();
                });

                // 2. FadeIn 완료 후 카운트다운 시작
                await _runningService.StartCountdownAsync();
        }
            finally
            {
                _isRunningStartInProgress = false;
            }
        }

        public void OnClickSetting()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.ShowPopup(EPopupUIType.Setting);
        }

        public void OnClickBack()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.RunWithFadeAsync(() => { UIManager.Instance.CloseCurrentPopup(); });
        }

        private void SetContentVisible(bool isVisible)
        {
            if (_contentCanvasGroup == null)
                return;

            _contentCanvasGroup.alpha = isVisible ? 1f : 0f;
            _contentCanvasGroup.blocksRaycasts = isVisible;
            _contentCanvasGroup.interactable = isVisible;
        }
    }
}
