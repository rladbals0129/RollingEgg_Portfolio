using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using RollingEgg.UI;
using RollingEgg.Util;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class UI_RunningResult : UI_Popup
    {
        [Header("## Egg Sprites")]
        [SerializeField] private List<EggSprite> _eggSprites = new();

        [Header("## JudgmentScore Texts")]
        [SerializeField] private List<JudgmentScoreText> _judgmentScoreTexts = new();

        [Header("## HP Score Text")]
        [SerializeField] private TMP_Text _hpCountText;
        [SerializeField] private TMP_Text _hpScoreText;

        [Header("## Combo Score Text")]
        [SerializeField] private TMP_Text _comboCountText;
        [SerializeField] private TMP_Text _comboScoreText;

        [Header("## Total Score Text")]
        [SerializeField] private TMP_Text _totalScoreText;

        [Header("## Rank Text")]
        [SerializeField] private TMP_Text _rankText;

        [Header("## Currency")]
        [SerializeField] private TMP_Text _commonCurrencyCountText;
        [SerializeField] private TMP_Text _specialCurrencyCountText;
        [SerializeField] private Image _specialCurrencyIcon;

        [Header("## Button")]
        [SerializeField] private Button _nextStageButton;

        private IAudioService _audioService;
        private IRunningService _runningService;

        public async override UniTask InitializeAsync()
        {
            _audioService = ServiceLocator.Get<IAudioService>();
            _runningService = ServiceLocator.Get<IRunningService>();

            foreach (var judgmentScoreText in _judgmentScoreTexts)
            {
                judgmentScoreText.Initialize();
            }

            await UniTask.Yield();
        }

        public void Bind(RunningScoreSnapshot scoreSnapshot, RewardResult rewardResult, bool isCleared, int eggId)
        {
            int resolvedEggId = eggId > 0 ? eggId : (_runningService?.CurrentEggId ?? 0);
            var eggSprite = _eggSprites.FirstOrDefault(es => es.EggId == resolvedEggId);
            if (eggSprite != null && _specialCurrencyIcon != null)
            {
                _specialCurrencyIcon.sprite = eggSprite.Icon;
            }

            var judgmentCounts = scoreSnapshot.judgmentCounts ?? new Dictionary<EJudgmentType, int>();
            var judgmentScores = scoreSnapshot.judgmentScores ?? new Dictionary<EJudgmentType, int>();

            // Judgment Score
            foreach (var judgmentScoreText in _judgmentScoreTexts)
            {
                int count = judgmentCounts.TryGetValue(judgmentScoreText.Type, out int value) ? value : 0;
                int judgmentScore = judgmentScores.TryGetValue(judgmentScoreText.Type, out int score) ? score : 0;
                judgmentScoreText.SetJudgmentText(count, judgmentScore);
            }

            // HP Score
            _hpCountText.text = $"{Mathf.RoundToInt(scoreSnapshot.currentHP)}/{Mathf.RoundToInt(scoreSnapshot.maxHP)}";
            _hpScoreText.text = $"{scoreSnapshot.hpScore}";

            // Combo Score
            _comboCountText.text = $"{scoreSnapshot.maxComboCount}/{scoreSnapshot.totalColorChangeCount}";
            _comboScoreText.text = $"{scoreSnapshot.comboBonus}";

            // Total Score
            _totalScoreText.text = $"{scoreSnapshot.totalScore}";

            // 클리어 등급
            _rankText.text = $"{scoreSnapshot.clearRank}";

            // 공용 재화 및 전용 재화
            _commonCurrencyCountText.text = $"{rewardResult.commonCurrency}";
            _specialCurrencyCountText.text = $"{rewardResult.specialCurrency}";

            // Update NextStageButton
            bool canProceed = isCleared && _runningService != null && !_runningService.IsMaxStage();
            _nextStageButton.gameObject.SetActive(canProceed);
        }

        public void OnClickRetry()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.CloseAllPopups();

            _runningService.OnReStartRunning();
        }

        public void OnClickLobby()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.CloseAllPopups();

            _runningService.Dispose();
            UIManager.Instance.ShowScene(ESceneUIType.Lobby);
        }

        public void OnClickNextStage()
        {
            _audioService.PlaySFXOneShot(ESFXKey.SFX_ButtonClick);

            UIManager.Instance.CloseAllPopups();

            _runningService.OnNextStage();
        }

    }

    [System.Serializable]
    public class EggSprite
    {
        [SerializeField] private int _eggId;
        [SerializeField] private Sprite _icon;

        public int EggId => _eggId;
        public Sprite Icon => _icon;
    }
}
