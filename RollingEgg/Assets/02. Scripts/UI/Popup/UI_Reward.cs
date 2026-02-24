using TMPro;
using UnityEngine;
using RollingEgg.UI;

namespace RollingEgg
{
    /// <summary>
    /// 러닝 보상 결과 팝업
    /// - 공용/전용 재화, 경험치 획득량을 표시하고 닫기 제공
    /// </summary>
    public class UI_Reward : UI_Popup
    {
        [Header("보상 표시")]
        [SerializeField] private TextMeshProUGUI _commonCurrencyText;
        [SerializeField] private TextMeshProUGUI _specialCurrencyText;
       

        public void Bind(int common, int special)     
        {
            if (_commonCurrencyText != null)
                _commonCurrencyText.text = $"공용 재화: {common}";
            if (_specialCurrencyText != null)
                _specialCurrencyText.text = $"전용 재화: {special}";
        }

        public void OnClickClose()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Hide();
                return;
            }

            uiManager.ClosePopup(this);
        }
    }
}

