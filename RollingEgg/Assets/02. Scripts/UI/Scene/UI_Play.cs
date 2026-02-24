using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using RollingEgg.UI;
using RollingEgg.Core;

namespace RollingEgg
{
    /// <summary>
    /// PC 16:9용 플레이 화면 UI
    /// - 러닝 진행/종료 지표(거리/색상별 거리/플레이타임/클리어 타임) 표시
    /// - 러닝 종료 이벤트를 구독하여 지표를 갱신
    /// </summary>
    public class UI_Play : UI_Scene
    {
        [Header("지표 표시")]
        [SerializeField] private TextMeshProUGUI _distanceText;
        [SerializeField] private TextMeshProUGUI _playTimeText;
        [SerializeField] private TextMeshProUGUI _clearFlagText;
        [SerializeField] private TextMeshProUGUI _clearTimeText;
        [SerializeField] private TextMeshProUGUI _colorYellowText;
        [SerializeField] private TextMeshProUGUI _colorBlueText;
        [SerializeField] private TextMeshProUGUI _colorBlackText;
        [SerializeField] private TextMeshProUGUI _colorRedText;
        [SerializeField] private TextMeshProUGUI _colorGreenText;

        private IEventBus _eventBus;
        private bool _isSceneChanging;

        public override async UniTask InitializeAsync()
        {
            // 이벤트 버스 참조 준비
            _eventBus = ServiceLocator.Get<IEventBus>();
            await UniTask.Yield();
        }

        public override void OnShow()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe<RunningGameCompletedEvent>(OnRunningCompleted);
            }

            // 초기값 표시
            SetDistance(0f);
            SetPlayTime(0f);
            SetCleared(false);
            SetClearTime(0f);
            SetColorDistances(new int[5]);
        }

        public override void OnHide()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<RunningGameCompletedEvent>(OnRunningCompleted);
            }
        }

        private void OnRunningCompleted(RunningGameCompletedEvent evt)
        {
            // 러닝 종료 시 마지막 지표를 표시
            SetDistance(evt.distance);
            SetPlayTime(evt.playTime);
            SetCleared(evt.isCleared);
            SetClearTime(evt.clearTime);
            SetColorDistances(evt.colorDistances);
        }

        private void SetDistance(float meters)
        {
            if (_distanceText != null)
                _distanceText.text = $"거리: {meters:0.0} m";
        }

        private void SetPlayTime(float seconds)
        {
            if (_playTimeText != null)
                _playTimeText.text = $"플레이타임: {seconds:0.0} s";
        }

        private void SetCleared(bool cleared)
        {
            if (_clearFlagText != null)
                _clearFlagText.text = cleared ? "클리어: O" : "클리어: X";
        }

        private void SetClearTime(float seconds)
        {
            if (_clearTimeText != null)
                _clearTimeText.text = seconds > 0f ? $"클리어 타임: {seconds:0.0} s" : "클리어 타임: -";
        }

        private void SetColorDistances(int[] colorDistances)
        {
            if (colorDistances == null || colorDistances.Length < 5)
                return;

            if (_colorYellowText != null) _colorYellowText.text = $"노랑: {colorDistances[0]} m";
            if (_colorBlueText != null) _colorBlueText.text = $"파랑: {colorDistances[1]} m";
            if (_colorBlackText != null) _colorBlackText.text = $"검정: {colorDistances[2]} m";
            if (_colorRedText != null) _colorRedText.text = $"빨강: {colorDistances[3]} m";
            if (_colorGreenText != null) _colorGreenText.text = $"초록: {colorDistances[4]} m";
        }

        // 타이틀/육성으로 전환 버튼 핸들러(버튼 연결용)
        public void OnClickBackToTitle()
        {
            ChangeSceneWithFade(ESceneUIType.Title).Forget();
        }

        public void OnClickGoToNurture()
        {
            ChangeSceneWithFade(ESceneUIType.Nurture).Forget();
        }

        private async UniTaskVoid ChangeSceneWithFade(ESceneUIType target)
        {
            if (_isSceneChanging)
                return;

            _isSceneChanging = true;
            try
            {
                await UIManager.Instance.ShowSceneWithFadeAsync(target);
            }
            finally
            {
                _isSceneChanging = false;
            }
        }
    }
}


