using System;
using RollingEgg.Core;
using RollingEgg.Data;
using RollingEgg.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg
{
    public class UI_StageItem : UI_Base
    {
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private TMP_Text _rankText;

        [SerializeField] private GameObject _closeObject;
        [SerializeField] private GameObject _rankPanel;

        private StageTableSO.StageRow _cachedRow;
        private Action<StageTableSO.StageRow> _onClick;
        private bool _isUnlocked;

        public void SetData(StageTableSO.StageRow row, StageProgressData progress, bool unlocked, Action<StageTableSO.StageRow> onClick)
        {
            _cachedRow = row;
            _onClick = onClick;
            _isUnlocked = unlocked;

            if (_closeObject != null)
                _closeObject.SetActive(!unlocked);

            if (_rankPanel != null)
                _rankPanel.SetActive(progress.IsCleared);

            if (_numberText != null)
                _numberText.text = $"{row.stageNumber}";

            if (unlocked)
            {
                if (_rankText != null)
                    _rankText.text = $"{progress.BestRank}";
            }
        }

        public void OnStageClick()
        {
            if (!_isUnlocked)
                return;

            _onClick?.Invoke(_cachedRow);
        }
    }
}






