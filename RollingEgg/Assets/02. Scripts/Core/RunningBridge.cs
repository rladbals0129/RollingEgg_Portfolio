using Cysharp.Threading.Tasks;
using UnityEngine;
using RollingEgg;

namespace RollingEgg.Core
{
    /// <summary>
    /// 러닝 파트와 육성 파트를 이어주는 브릿지 컴포넌트
    /// - RunningGameCompletedEvent를 구독하여 보상/경험치를 반영
    /// </summary>
    public class RunningBridge : MonoBehaviour
    {
        private IEventBus _eventBus;
        private ICurrencyService _currencyService;
        // private IExperienceService _experienceService;
        private IGrowthActionService _growthActionService;
        private IRunningService _runningService;
        private IStageService _stageService;
        private UI.UIManager _ui;

        private void Awake()
        {
            _eventBus = ServiceLocator.Get<IEventBus>();
            _currencyService = ServiceLocator.Get<ICurrencyService>();
            _growthActionService = ServiceLocator.Get<IGrowthActionService>();
            _runningService = ServiceLocator.Get<IRunningService>();
            _stageService = ServiceLocator.Get<IStageService>();
            _ui = UI.UIManager.Instance;

            _eventBus.Subscribe<RunningGameCompletedEvent>(OnRunningCompleted);
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<RunningGameCompletedEvent>(OnRunningCompleted);
        }

        private async void OnRunningCompleted(RunningGameCompletedEvent evt)
        {
            if (!evt.isCleared)
            {
                Debug.Log("[RunningBridge] 러닝 실패 - 보상 처리 생략");
                return;
            }

            // Stage 클리어 정보 저장
            if (evt.stageId > 0 && _stageService != null)
            {
                _stageService.UpdateStageResult(evt.stageId, evt.score.totalScore, evt.score.clearRank);
                await _stageService.SaveDataAsync();
                Debug.Log($"[RunningBridge] Stage {evt.stageId} 클리어 정보 저장 완료 (Score: {evt.score.totalScore}, Rank: {evt.score.clearRank})");
            }

            int eggId = evt.eggId;
            string eggType = evt.eggType;

            if (eggId <= 0 && _runningService != null)
            {
                eggId = _runningService.CurrentEggId;
            }

            if (string.IsNullOrEmpty(eggType) && _growthActionService != null && eggId > 0 && _growthActionService.HasEgg(eggId))
            {
                eggType = _growthActionService.GetEggType(eggId);
            }

            var context = new RunningRewardContext
            {
                eggId = eggId,
                eggType = eggType,
                isCleared = evt.isCleared,
                totalScore = evt.score.totalScore,
                rank = evt.score.clearRank
            };

            var reward = await _currencyService.ProcessRunningRewardAsync(context);

            var popup = _ui.ShowPopup(UI.EPopupUIType.RunningResult) as UI_RunningResult;
            if (popup != null)
            {
                popup.Bind(evt.score, reward, evt.isCleared, eggId);
            }
        }
    }
}


